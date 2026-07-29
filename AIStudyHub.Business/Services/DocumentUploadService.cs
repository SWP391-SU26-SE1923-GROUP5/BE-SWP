using System.Data;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Services;

public sealed class DocumentUploadService : IDocumentUploadService
{
    private const string ActiveFileNameIndex = "UX_Document_UserId_FileName_Active";
    private const int FileNameSaveAttempts = 3;
    private const long BytesPerMegabyte = 1024L * 1024L;
    private const string UploadUrlPrefix = "/uploads/";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentService _documentService;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentProcessingQueue _processingQueue;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly DocumentStorageOptions _storageOptions;
    private readonly ILogger<DocumentUploadService> _logger;

    public DocumentUploadService(
        IUnitOfWork unitOfWork,
        ApplicationDbContext dbContext,
        IDocumentService documentService,
        IFileStorageService fileStorage,
        IDocumentProcessingQueue processingQueue,
        IVectorStoreService vectorStoreService,
        IOptions<DocumentStorageOptions> storageOptions,
        ILogger<DocumentUploadService> logger)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _documentService = documentService;
        _fileStorage = fileStorage;
        _processingQueue = processingQueue;
        _vectorStoreService = vectorStoreService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<UploadDocumentResponseDto> UploadAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var user = await _unitOfWork.Users.GetByIdAsync(
            request.UserId,
            cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        var subjectExists = await _unitOfWork.Subjects.Query()
            .AnyAsync(
                subject => subject.Id == request.SubjectId
                    && subject.OwnerUserId == request.UserId,
                cancellationToken);
        if (!subjectExists)
            throw new KeyNotFoundException("Subject not found.");

        var safeFileName = Path.GetFileName(request.FileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!_fileStorage.IsValidExtension(extension))
            throw new ValidationException($"File extension '{extension}' is not allowed.");

        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(
            user.TierId,
            cancellationToken);
        if (tier is null)
            throw new InvalidDataException("User tier is not configured.");

        var tierLimitBytes = (long)tier.StorageLimitMb * BytesPerMegabyte;
        var activeBytes = await GetActiveBytesAsync(
            request.UserId,
            cancellationToken);
        EnsureWithinQuota(
            activeBytes,
            tierLimitBytes,
            request.ContentLength);

        StoredFileResult? storedFile = null;
        var documentWasCommitted = false;

        try
        {
            storedFile = await _fileStorage.SaveFileAsync(
                request.Content,
                Path.GetFileNameWithoutExtension(safeFileName),
                extension,
                _storageOptions.MaxFileSizeBytes,
                cancellationToken);

            Document document;
            await using (var transaction =
                         await _dbContext.Database.BeginTransactionAsync(
                             IsolationLevel.Serializable,
                             cancellationToken))
            {
                activeBytes = await GetActiveBytesAsync(
                    request.UserId,
                    cancellationToken);
                EnsureWithinQuota(
                    activeBytes,
                    tierLimitBytes,
                    storedFile.SizeBytes);

                var newActiveBytes = checked(
                    activeBytes + storedFile.SizeBytes);
                document = new Document
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    SubjectId = request.SubjectId,
                    Title = request.Title,
                    FileLink = _fileStorage.GetFileUrl(
                        storedFile.RelativePath),
                    FileExtension = extension,
                    FileType = request.ContentType,
                    FileSizeBytes = storedFile.SizeBytes,
                    ShareStatus = "private",
                    Status = DocumentStatus.Processing,
                    LifecycleStatus = DocumentLifecycleStatus.Active
                };

                await _unitOfWork.Documents.AddAsync(
                    document,
                    cancellationToken);
                user.CurrentStorageCapacity = checked(
                    (int)Math.Ceiling(
                        newActiveBytes / (double)BytesPerMegabyte));
                _unitOfWork.Users.Update(user);

                await SaveWithAvailableFileNameAsync(
                    document,
                    safeFileName,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                documentWasCommitted = true;
            }

            var processRequest = new DocumentProcessRequest(
                document.Id,
                request.UserId,
                _fileStorage.ResolveFullPath(storedFile.RelativePath),
                document.FileName!,
                request.ContentType);

            if (!_processingQueue.TryEnqueue(processRequest))
            {
                _logger.LogWarning(
                    "Document {DocumentId} was committed but could not be queued immediately",
                    document.Id);
            }

            return new UploadDocumentResponseDto(
                document.Id,
                "processing",
                0,
                "Document is being processed in the background");
        }
        catch
        {
            if (storedFile is not null && !documentWasCommitted)
            {
                await _fileStorage.DeleteFileAsync(
                    storedFile.RelativePath,
                    CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<UploadDocumentResponseDto> ReprocessAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("Authentication is required.");

        var document = await _unitOfWork.Documents.Query()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == documentId
                    && candidate.UserId == userId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");

        var relativePath = GetStoredRelativePath(document.FileLink);
        string fullPath;
        try
        {
            fullPath = _fileStorage.ResolveFullPath(relativePath);
        }
        catch (InvalidOperationException)
        {
            throw new ValidationException(
                "Document has no valid stored source file.");
        }

        if (!File.Exists(fullPath))
            throw new ValidationException("Document source file is missing.");

        await _vectorStoreService.DeleteVectorsByDocumentIdAsync(documentId);

        document.Status = DocumentStatus.Processing;
        document.ErrorMessage = null;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var processRequest = new DocumentProcessRequest(
            document.Id,
            userId,
            fullPath,
            document.FileName ?? "unknown",
            document.FileType ?? "application/octet-stream");

        if (!_processingQueue.TryEnqueue(processRequest))
        {
            _logger.LogWarning(
                "Document {DocumentId} was committed for reprocessing but could not be queued immediately",
                document.Id);
        }

        return new UploadDocumentResponseDto(
            document.Id,
            "processing",
            0,
            "Re-processing in progress");
    }

    private void ValidateRequest(DocumentUploadRequest request)
    {
        if (request.UserId == Guid.Empty)
            throw new UnauthorizedAccessException("Authentication is required.");
        if (request.ContentLength <= 0)
            throw new ValidationException("A non-empty file is required.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Document title is required.");
        if (request.SubjectId == Guid.Empty)
            throw new ValidationException("Subject id is required.");
        if (request.ContentLength > _storageOptions.MaxFileSizeBytes)
            throw new FileSizeLimitExceededException(
                request.ContentLength,
                _storageOptions.MaxFileSizeBytes);
    }

    private async Task<long> GetActiveBytesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.Documents.Query()
            .Where(document => document.UserId == userId
                && document.LifecycleStatus != DocumentLifecycleStatus.Purged)
            .SumAsync(
                document => (long?)document.FileSizeBytes,
                cancellationToken)
            ?? 0L;
    }

    private static void EnsureWithinQuota(
        long currentBytes,
        long limitBytes,
        long requestedBytes)
    {
        if (currentBytes > limitBytes
            || requestedBytes > limitBytes - currentBytes)
        {
            throw new StorageQuotaExceededException(
                currentBytes,
                limitBytes,
                requestedBytes);
        }
    }

    private async Task SaveWithAvailableFileNameAsync(
        Document document,
        string requestedFileName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= FileNameSaveAttempts; attempt++)
        {
            document.FileName = await _documentService.GetAvailableFileNameAsync(
                document.UserId,
                requestedFileName,
                cancellationToken: cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception)
                when (IsActiveFileNameConflict(exception))
            {
                if (attempt == FileNameSaveAttempts)
                {
                    throw new InvalidOperationException(
                        $"Could not allocate a unique document filename after {FileNameSaveAttempts} attempts.",
                        exception);
                }
            }
        }
    }

    private static bool IsActiveFileNameConflict(
        DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains(
            ActiveFileNameIndex,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetStoredRelativePath(string? fileLink)
    {
        if (string.IsNullOrWhiteSpace(fileLink)
            || !fileLink.StartsWith(
                UploadUrlPrefix,
                StringComparison.Ordinal))
        {
            throw new ValidationException(
                "Document has no stored source file.");
        }

        var relativePath = fileLink[UploadUrlPrefix.Length..];
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ValidationException("Document has no stored source file.");

        return relativePath;
    }
}

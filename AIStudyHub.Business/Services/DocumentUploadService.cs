using System.Data;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Rag;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentProcessingQueue _processingQueue;
    private readonly DocumentStorageOptions _storageOptions;
    private readonly ILogger<DocumentUploadService> _logger;

    public DocumentUploadService(
        IUnitOfWork unitOfWork,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IFileStorageService fileStorage,
        IDocumentProcessingQueue processingQueue,
        IOptions<DocumentStorageOptions> storageOptions,
        ILogger<DocumentUploadService> logger)
    {
        _unitOfWork = unitOfWork;
        _dbContextFactory = dbContextFactory;
        _fileStorage = fileStorage;
        _processingQueue = processingQueue;
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

        var documentId = Guid.NewGuid();
        StoredFileResult? storedFile = null;
        var documentCommitVerified = false;

        try
        {
            storedFile = await _fileStorage.SaveFileAsync(
                request.Content,
                Path.GetFileNameWithoutExtension(safeFileName),
                extension,
                _storageOptions.MaxFileSizeBytes,
                cancellationToken);

            if (storedFile.SizeBytes <= 0)
                throw new ValidationException("A non-empty file is required.");

            var document = new Document
            {
                Id = documentId,
                UserId = request.UserId,
                SubjectId = request.SubjectId,
                Title = request.Title,
                FileLink = _fileStorage.GetFileUrl(storedFile.RelativePath),
                FileExtension = extension,
                FileType = request.ContentType,
                FileSizeBytes = storedFile.SizeBytes,
                ShareStatus = "private",
                Status = DocumentStatus.Processing,
                LifecycleStatus = DocumentLifecycleStatus.Active
            };

            cancellationToken.ThrowIfCancellationRequested();
            await PersistUploadAsync(
                document,
                safeFileName,
                tierLimitBytes);
            documentCommitVerified = true;

            var processRequest = new DocumentProcessRequest(
                documentId,
                request.UserId,
                _fileStorage.ResolveFullPath(storedFile.RelativePath),
                document.FileName!,
                request.ContentType);

            if (!_processingQueue.TryEnqueue(processRequest))
            {
                _logger.LogWarning(
                    "Document {DocumentId} was committed but could not be queued immediately",
                    documentId);
            }

            var readiness = DocumentReadinessEvaluator.Evaluate(document);
            return new UploadDocumentResponseDto(
                document.Id,
                readiness.Status,
                0,
                readiness.Message,
                readiness.IsChatReady,
                readiness.CanRetry);
        }
        catch
        {
            if (storedFile is not null && !documentCommitVerified)
            {
                var commitState = await TryGetDocumentCommitStateAsync(
                    documentId);
                if (commitState == false)
                {
                    await _fileStorage.DeleteFileAsync(
                        storedFile.RelativePath,
                        CancellationToken.None);
                }
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

        document.Status = DocumentStatus.Processing;
        document.ErrorMessage = null;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var processRequest = new DocumentProcessRequest(
            document.Id,
            userId,
            fullPath,
            document.FileName ?? "unknown",
            document.FileType ?? "application/octet-stream",
            IsReprocess: true);

        if (!_processingQueue.TryEnqueue(processRequest))
        {
            _logger.LogWarning(
                "Document {DocumentId} was committed for reprocessing but could not be queued immediately",
                document.Id);
        }

        var readiness = DocumentReadinessEvaluator.Evaluate(document);
        return new UploadDocumentResponseDto(
            document.Id,
            readiness.Status,
            0,
            readiness.Message,
            readiness.IsChatReady,
            readiness.CanRetry);
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

    private async Task PersistUploadAsync(
        Document document,
        string requestedFileName,
        long tierLimitBytes)
    {
        await using var context =
            await _dbContextFactory.CreateDbContextAsync(
                CancellationToken.None);
        context.Documents.Add(document);

        var strategy = new SqlServerRetryingExecutionStrategy(
            context,
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(2),
            errorNumbersToAdd: (ICollection<int>?)null);
        await strategy.ExecuteInTransactionAsync(
            operation: async transactionCancellationToken =>
            {
                var activeBytes = await context.Documents
                    .Where(candidate => candidate.UserId == document.UserId
                        && candidate.LifecycleStatus
                            != DocumentLifecycleStatus.Purged)
                    .SumAsync(
                        candidate => (long?)candidate.FileSizeBytes,
                        transactionCancellationToken)
                    ?? 0L;
                EnsureWithinQuota(
                    activeBytes,
                    tierLimitBytes,
                    document.FileSizeBytes);

                var user = await context.Users.SingleOrDefaultAsync(
                    candidate => candidate.Id == document.UserId,
                    transactionCancellationToken);
                if (user is null)
                {
                    throw new UnauthorizedAccessException(
                        "Authentication is required.");
                }

                var newActiveBytes = checked(
                    activeBytes + document.FileSizeBytes);
                user.CurrentStorageCapacity = checked(
                    (int)Math.Ceiling(
                        newActiveBytes / (double)BytesPerMegabyte));

                await SaveWithAvailableFileNameAsync(
                    context,
                    document,
                    requestedFileName,
                    transactionCancellationToken);
            },
            verifySucceeded: _ => DocumentExistsAsync(document.Id),
            isolationLevel: IsolationLevel.Serializable,
            cancellationToken: CancellationToken.None);

        context.ChangeTracker.AcceptAllChanges();
    }

    private async Task SaveWithAvailableFileNameAsync(
        ApplicationDbContext context,
        Document document,
        string requestedFileName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= FileNameSaveAttempts; attempt++)
        {
            document.FileName = await GetAvailableFileNameAsync(
                context,
                document.UserId,
                requestedFileName,
                cancellationToken);

            try
            {
                await context.SaveChangesAsync(
                    acceptAllChangesOnSuccess: false,
                    cancellationToken);
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

    private static async Task<string> GetAvailableFileNameAsync(
        ApplicationDbContext context,
        Guid userId,
        string fileName,
        CancellationToken cancellationToken)
    {
        const int maxFileNameLength = 255;
        var normalizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName))
        {
            throw new ArgumentException(
                "Document filename is required.",
                nameof(fileName));
        }

        var extension = Path.GetExtension(normalizedFileName);
        var stem = Path.GetFileNameWithoutExtension(normalizedFileName);
        if (string.IsNullOrWhiteSpace(stem)
            || extension.Length >= maxFileNameLength)
        {
            throw new ArgumentException(
                "Document filename is invalid.",
                nameof(fileName));
        }

        var existingNames = new HashSet<string>(
            await context.Documents
                .Where(candidate => candidate.UserId == userId
                    && candidate.LifecycleStatus
                        == DocumentLifecycleStatus.Active
                    && candidate.FileName != null)
                .Select(candidate => candidate.FileName!)
                .ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var initialStemLength = Math.Min(
            stem.Length,
            maxFileNameLength - extension.Length);
        var candidateName = stem[..initialStemLength] + extension;
        if (!existingNames.Contains(candidateName))
            return candidateName;

        for (var suffixNumber = 1; ; suffixNumber++)
        {
            var suffix = $" ({suffixNumber})";
            var maxStemLength =
                maxFileNameLength - extension.Length - suffix.Length;
            if (maxStemLength <= 0)
            {
                throw new ArgumentException(
                    "Document filename is too long.",
                    nameof(fileName));
            }

            candidateName =
                stem[..Math.Min(stem.Length, maxStemLength)]
                + suffix
                + extension;
            if (!existingNames.Contains(candidateName))
                return candidateName;
        }
    }

    private Task<bool> DocumentExistsAsync(Guid documentId)
    {
        return DocumentExistsCoreAsync(documentId);
    }

    private async Task<bool> DocumentExistsCoreAsync(Guid documentId)
    {
        await using var context =
            await _dbContextFactory.CreateDbContextAsync(
                CancellationToken.None);
        return await context.Documents
            .AsNoTracking()
            .AnyAsync(
                document => document.Id == documentId,
                CancellationToken.None);
    }

    private async Task<bool?> TryGetDocumentCommitStateAsync(
        Guid documentId)
    {
        try
        {
            return await DocumentExistsAsync(documentId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not verify commit state for document {DocumentId}; preserving stored file",
                documentId);
            return null;
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

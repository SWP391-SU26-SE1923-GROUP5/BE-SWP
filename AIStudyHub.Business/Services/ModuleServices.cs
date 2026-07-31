using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using AIStudyHub.Business.DTOs.Answers;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.DTOs.Gamification;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.DTOs.Payments;
using AIStudyHub.Business.DTOs.Questions;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.DTOs.QuizSubmissions;
using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.DTOs.Subjects;
using AIStudyHub.Business.DTOs.TierMemberships;
using AIStudyHub.Business.DTOs.Common;
using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIStudyHub.Business.Services;

public sealed class DocumentService : IDocumentService
{
    private const string ActiveFileNameIndex = "UX_Document_UserId_FileName_Active";
    private const int FileNameSaveAttempts = 3;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IVectorStoreService? _vectorStoreService;

    public DocumentService(IUnitOfWork unitOfWork, IMapper mapper, IVectorStoreService? vectorStoreService = null)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _vectorStoreService = vectorStoreService;
    }

    private static DocumentResponseDto MapToDto(Document d) => new(
        d.Id, d.UserId, d.SubjectId, d.Title, d.FileLink, d.FileName, d.FileExtension,
        d.FileType, d.FileSizeBytes, d.ShareStatus, d.Status, d.ErrorMessage,
        d.Votes.Sum(v => v.Type == AIStudyHub.Data.Enums.VoteType.Upvote ? 1 : -1),
        d.LifecycleStatus, d.TrashedAt, d.CreatedAt, d.UpdatedAt);

    private static DocumentResponseDto MapToDtoNoVotes(Document d) => new(
        d.Id, d.UserId, d.SubjectId, d.Title, d.FileLink, d.FileName, d.FileExtension,
        d.FileType, d.FileSizeBytes, d.ShareStatus, d.Status, d.ErrorMessage,
        d.Votes.Count, d.LifecycleStatus, d.TrashedAt, d.CreatedAt, d.UpdatedAt);

    public async Task<string> GetAvailableFileNameAsync(
        Guid userId,
        string fileName,
        Guid? excludeDocumentId = null,
        CancellationToken cancellationToken = default)
    {
        const int maxFileNameLength = 255;
        var normalizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName))
            throw new ArgumentException("Document filename is required.", nameof(fileName));

        var extension = Path.GetExtension(normalizedFileName);
        var stem = Path.GetFileNameWithoutExtension(normalizedFileName);
        if (string.IsNullOrWhiteSpace(stem) || extension.Length >= maxFileNameLength)
            throw new ArgumentException("Document filename is invalid.", nameof(fileName));

        var query = _unitOfWork.Documents.Query()
            .Where(d => d.UserId == userId
                        && d.LifecycleStatus == DocumentLifecycleStatus.Active
                        && d.FileName != null);

        if (excludeDocumentId.HasValue)
            query = query.Where(d => d.Id != excludeDocumentId.Value);

        var existingNames = new HashSet<string>(
            await query.Select(d => d.FileName!).ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var initialStemLength = Math.Min(stem.Length, maxFileNameLength - extension.Length);
        var candidate = stem[..initialStemLength] + extension;
        if (!existingNames.Contains(candidate))
            return candidate;

        for (var suffixNumber = 1; ; suffixNumber++)
        {
            var suffix = $" ({suffixNumber})";
            var maxStemLength = maxFileNameLength - extension.Length - suffix.Length;
            if (maxStemLength <= 0)
                throw new ArgumentException("Document filename is too long.", nameof(fileName));

            candidate = stem[..Math.Min(stem.Length, maxStemLength)] + suffix + extension;
            if (!existingNames.Contains(candidate))
                return candidate;
        }
    }

    public async Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<DocumentResponseDto>> GetAllPagedAsync(
        Guid userId,
        AIStudyHub.Business.DTOs.Common.PaginationParams @params,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Documents.Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Include(d => d.Votes)
            .Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active
                        && (d.UserId == userId || d.ShareStatus == "public"
                            || _unitOfWork.DocumentShares.Query().Any(s => s.DocumentId == d.Id && s.UserId == userId)))
            .AsNoTracking();

        if (subjectId.HasValue)
            query = query.Where(d => d.SubjectId == subjectId.Value);

        if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
        {
            var search = @params.SearchTerm.ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(search)
                                    || (d.Subject != null && d.Subject.SubjectName.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(@params.SortBy))
            query = @params.IsDescending
                ? query.OrderByDescending(d => EF.Property<object>(d, @params.SortBy))
                : query.OrderBy(d => EF.Property<object>(d, @params.SortBy));
        else
            query = query.OrderByDescending(d => d.CreatedAt);

        var items = await query.Skip(@params.Offset).Take(@params.Limit).ToListAsync(cancellationToken);
        var dtos = items.Select(MapToDto).ToList();

        return new AIStudyHub.Business.DTOs.Common.PagedResultDto<DocumentResponseDto>(dtos, totalCount, @params.Offset, @params.Limit);
    }

    public async Task<IReadOnlyList<DocumentResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Include(d => d.Votes)
            .Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return documents.Select(MapToDtoNoVotes).ToList();
    }

    public async Task<IReadOnlyList<DocumentResponseDto>> GetAllByUserIdAsync(
        Guid userId, string? keyword = null, Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Documents.Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Include(d => d.Votes)
            .Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active
                        && (d.UserId == userId || d.ShareStatus == "public"
                            || _unitOfWork.DocumentShares.Query().Any(s => s.DocumentId == d.Id && s.UserId == userId)));

        if (subjectId.HasValue)
            query = query.Where(d => d.SubjectId == subjectId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(lower)
                                     || (d.FileName != null && d.FileName.ToLower().Contains(lower)));
        }

        var documents = await query.AsNoTracking().ToListAsync(cancellationToken);
        return documents.Select(MapToDtoNoVotes).ToList();
    }

    public async Task<DocumentResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Include(d => d.Votes)
            .Where(d => d.LifecycleStatus == DocumentLifecycleStatus.Active)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return document is null ? null : MapToDtoNoVotes(document);
    }

    public async Task<DocumentResponseDto> CreateAsync(CreateDocumentRequestDto request, CancellationToken cancellationToken = default)
    {
        var subjectExists = await _unitOfWork.Subjects.Query()
            .AnyAsync(
                subject => subject.Id == request.SubjectId
                    && subject.OwnerUserId == request.UserId,
                cancellationToken);
        if (!subjectExists)
            throw new KeyNotFoundException("Subject not found.");

        var document = _mapper.Map<Data.Entities.Document>(request);
        document.LifecycleStatus = DocumentLifecycleStatus.Active;
        await _unitOfWork.Documents.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == document.Id, cancellationToken);

        return _mapper.Map<DocumentResponseDto>(created);
    }

    public async Task<DocumentResponseDto> UpdateAsync(Guid id, UpdateDocumentRequestDto request, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {id} not found.");

        _mapper.Map(request, document);
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return _mapper.Map<DocumentResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {id} not found.");

        if (_vectorStoreService != null)
            await _vectorStoreService.DeleteVectorsByDocumentIdAsync(id);

        _unitOfWork.Documents.Remove(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShareDocumentResponseDto> ShareDocumentAsync(
        Guid documentId,
        Guid callerId,
        ShareDocumentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        var isOwner = document.UserId == callerId;
        if (!isOwner)
        {
            var callerShare = await _unitOfWork.DocumentShares
                .Query()
                .Where(s => s.DocumentId == documentId && s.UserId == callerId)
                .AnyAsync(cancellationToken);
            if (!callerShare)
                throw new UnauthorizedAccessException("Only the document owner or a collaborator can change sharing settings.");
        }

        var allRequested = request.SharedUserIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();

        var callerInRequest = allRequested.Contains(callerId);
        var ownerInRequest = allRequested.Contains(document.UserId);

        if (callerInRequest && ownerInRequest)
            throw new InvalidOperationException("Cannot share with yourself or the document owner.");
        if (callerInRequest)
            throw new InvalidOperationException("Cannot share with yourself.");
        if (ownerInRequest)
            throw new InvalidOperationException("Cannot modify the owner's access level.");

        var targetIds = allRequested.Where(id => id != callerId && id != document.UserId).ToList();

        if (targetIds.Count > 0)
        {
            var existingIds = await _unitOfWork.Users
                .Query()
                .Where(u => targetIds.Contains(u.Id) && u.IsActive && u.Status == "active")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var notFoundIds = targetIds.Except(existingIds).ToList();
            if (notFoundIds.Count > 0)
                throw new KeyNotFoundException(
                    $"User(s) not found or not active: {string.Join(", ", notFoundIds)}.");

            targetIds = existingIds;
        }

        // Load existing shares for this document.
        var existingShares = await _unitOfWork.DocumentShares
            .Query()
            .Where(s => s.DocumentId == documentId)
            .ToListAsync(cancellationToken);
        var existingByUserId = existingShares.ToDictionary(s => s.UserId);

        var levels = request.Levels ?? Enumerable.Repeat((int)ShareLevel.Read, targetIds.Count).ToList();
        var resultLevels = new List<int>();

        // Upsert: update existing shares or insert new ones, in the order of targetIds.
        for (int i = 0; i < targetIds.Count; i++)
        {
            var targetId = targetIds[i];
            var requestedLevel = i < levels.Count ? (ShareLevel)levels[i] : ShareLevel.Read;

            // Validate requested level enum.
            if (requestedLevel != ShareLevel.Read && requestedLevel != ShareLevel.Edit)
                requestedLevel = ShareLevel.Read;

            if (existingByUserId.TryGetValue(targetId, out var existingShare))
            {
                existingShare.Level = requestedLevel;
                existingShare.SharedBy = callerId;
                existingShare.SharedAt = DateTime.UtcNow;
                _unitOfWork.DocumentShares.Update(existingShare);
                resultLevels.Add((int)existingShare.Level);
            }
            else
            {
                await _unitOfWork.DocumentShares.AddAsync(new DocumentShare
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    UserId = targetId,
                    Level = requestedLevel,
                    SharedBy = callerId,
                    SharedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
                resultLevels.Add((int)requestedLevel);
            }
        }

        var remainingShares = await _unitOfWork.DocumentShares
            .Query()
            .Where(s => s.DocumentId == documentId)
            .CountAsync(cancellationToken);

        document.ShareStatus = remainingShares > 0 ? "shared" : "private";

        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ShareDocumentResponseDto(document.Id, targetIds, resultLevels);
    }

    public async Task<IReadOnlyList<DocumentResponseDto>> GetTrashAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var docs = await _unitOfWork.Documents
            .Query()
            .Include(d => d.Subject)
            .Include(d => d.User)
            .Include(d => d.Votes)
            .Where(d => d.UserId == userId && d.LifecycleStatus == DocumentLifecycleStatus.Trashed)
            .OrderByDescending(d => d.TrashedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return docs.Select(MapToDtoNoVotes).ToList();
    }

    public async Task TrashAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        if (document.UserId != userId)
            throw new UnauthorizedAccessException("Only the document owner can trash it.");

        if (document.LifecycleStatus == DocumentLifecycleStatus.Trashed)
            return; // already trashed — idempotent

        if (_vectorStoreService != null)
            await _vectorStoreService.DeleteVectorsByDocumentIdAsync(documentId);

        document.LifecycleStatus = DocumentLifecycleStatus.Trashed;
        document.TrashedAt = DateTime.UtcNow;
        document.TrashedBy = userId;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        if (document.UserId != userId)
            throw new UnauthorizedAccessException("Only the document owner can restore it.");

        if (document.LifecycleStatus == DocumentLifecycleStatus.Purged)
            throw new InvalidOperationException("A purged document cannot be restored.");

        var requestedFileName = document.FileName ?? "document";
        for (var attempt = 1; attempt <= FileNameSaveAttempts; attempt++)
        {
            document.FileName = await GetAvailableFileNameAsync(
                userId,
                requestedFileName,
                document.Id,
                cancellationToken);
            document.LifecycleStatus = DocumentLifecycleStatus.Active;
            document.TrashedAt = null;
            document.TrashedBy = null;
            _unitOfWork.Documents.Update(document);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (IsActiveFileNameConflict(ex))
            {
                if (attempt == FileNameSaveAttempts)
                {
                    throw new InvalidOperationException(
                        $"Could not allocate a unique document filename after {FileNameSaveAttempts} attempts.", ex);
                }
            }
        }
    }

    private static bool IsActiveFileNameConflict(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains(ActiveFileNameIndex, StringComparison.OrdinalIgnoreCase);
    }

    public async Task PurgeAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        if (document.UserId != userId)
            throw new UnauthorizedAccessException("Only the document owner can purge it.");

        if (document.LifecycleStatus != DocumentLifecycleStatus.Trashed)
            throw new InvalidOperationException("Only trashed documents can be permanently purged.");

        if (_vectorStoreService != null)
            await _vectorStoreService.DeleteVectorsByDocumentIdAsync(documentId);

        _unitOfWork.Documents.Remove(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentShareListDto> GetSharesAsync(Guid documentId, Guid callerId, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        bool isOwner = document.UserId == callerId;

        // Non-owner callers must at least have a share entry to view shares.
        // Owners always see the full list; non-owners only see their own entry.
        var sharesQuery = _unitOfWork.DocumentShares
            .Query()
            .Include(s => s.User)
            .Where(s => s.DocumentId == documentId);

        if (!isOwner)
        {
            sharesQuery = sharesQuery.Where(s => s.UserId == callerId);

            var hasShare = await sharesQuery.AnyAsync(cancellationToken);
            if (!hasShare)
                throw new UnauthorizedAccessException("You do not have access to this document's shares.");
        }

        var shares = await sharesQuery
            .OrderBy(s => s.SharedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = shares.Select(s => new DocumentShareDto(
            s.Id, s.DocumentId, s.UserId,
            s.User?.FullName ?? string.Empty,
            s.Level, s.SharedAt)).ToList();

        return new DocumentShareListDto(documentId, dtos);
    }

    public async Task RevokeShareAsync(Guid documentId, Guid targetUserId, Guid callerId, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        if (document.UserId != callerId)
            throw new UnauthorizedAccessException("Only the document owner can revoke shares.");

        var share = await _unitOfWork.DocumentShares
            .Query()
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.UserId == targetUserId, cancellationToken);

        if (share is null) return; // idempotent

        _unitOfWork.DocumentShares.Remove(share);

        var remaining = await _unitOfWork.DocumentShares
            .Query()
            .Where(s => s.DocumentId == documentId && s.Id != share.Id)
            .CountAsync(cancellationToken);

        if (remaining == 0)
        {
            document.ShareStatus = "private";
            _unitOfWork.Documents.Update(document);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class VoteService : IVoteService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotificationService? _realTimeNotifier;
    private readonly ILogger<VoteService>? _logger;

    public VoteService(IUnitOfWork unitOfWork, IMapper mapper,
        IRealTimeNotificationService? realTimeNotifier = null,
        ILogger<VoteService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VoteResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var votes = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return votes.Select(_mapper.Map<VoteResponseDto>).ToList();
    }

    public async Task<VoteResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vote = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        return vote is null ? null : _mapper.Map<VoteResponseDto>(vote);
    }

    public async Task<VoteResponseDto?> GetByUserAndDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var vote = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId && v.DocumentId == documentId, cancellationToken);

        return vote is null ? null : _mapper.Map<VoteResponseDto>(vote);
    }

    public async Task<VoteResponseDto> CreateVoteAsync(Guid userId, Guid documentId, VoteType type, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Votes
            .Query()
            .FirstOrDefaultAsync(v => v.UserId == userId && v.DocumentId == documentId, cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException("User has already voted on this document.");
        }

        var documentExists = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        }

        var vote = new Data.Entities.Vote
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentId = documentId,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Votes.AddAsync(vote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Votes
            .Query()
            .Include(v => v.User)
            .Include(v => v.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vote.Id, cancellationToken);

        // Real-time vote-received push to the document owner (skip self-votes).
        if (_realTimeNotifier is not null && created is not null && created.Document is not null
            && created.Document.UserId != userId)
        {
            try
            {
                await _realTimeNotifier.NotifyVoteReceivedAsync(
                    created.Document.UserId,
                    userId,
                    documentId,
                    created.Document.Title ?? "Document",
                    type,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Vote-received real-time notify failed for document {DocumentId}", documentId);
            }
        }

        return _mapper.Map<VoteResponseDto>(created);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vote = await _unitOfWork.Votes.GetByIdAsync(id, cancellationToken);
        if (vote is null)
        {
            throw new KeyNotFoundException($"Vote with ID {id} not found.");
        }

        _unitOfWork.Votes.Remove(vote);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotificationService? _realTimeNotifier;
    private readonly ILogger<ReportService>? _logger;

    public ReportService(IUnitOfWork unitOfWork, IMapper mapper,
        IRealTimeNotificationService? realTimeNotifier = null,
        ILogger<ReportService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReportResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var reports = await _unitOfWork.Reports
            .Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .Include(r => r.ResolvedByUser)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reports.Select(_mapper.Map<ReportResponseDto>).ToList();
    }

    public async Task<ReportResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports
            .Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .Include(r => r.ResolvedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return report is null ? null : _mapper.Map<ReportResponseDto>(report);
    }

    public async Task<ReportResponseDto> CreateWithUserIdAsync(CreateReportRequestDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        if (document.IsNonFlaggable)
        {
            throw new InvalidOperationException("This document is marked as non-flaggable.");
        }

        // 24h cooldown: prevent same user from reporting the same document more than once per day
        var cooldownCutoff = DateTime.UtcNow.AddHours(-24);
        var hasRecentReport = await _unitOfWork.Reports.Query()
            .AnyAsync(r => r.UserId == userId
                        && r.DocumentId == request.DocumentId
                        && r.CreatedAt >= cooldownCutoff, cancellationToken);
        if (hasRecentReport)
        {
            throw new QuotaExceededException("You have already reported this document within the last 24 hours. Please wait before submitting another report.");
        }

        var existingPending = await _unitOfWork.Reports.Query()
            .AnyAsync(r => r.UserId == userId && r.DocumentId == request.DocumentId && r.Status == AIStudyHub.Data.Enums.ReportStatus.Pending, cancellationToken);
        if (existingPending)
        {
            throw new InvalidOperationException("You already have a pending report for this document.");
        }

        var report = new Data.Entities.Report
        {
            UserId = userId,
            DocumentId = request.DocumentId,
            Category = (AIStudyHub.Data.Enums.ReportCategory)request.Category,
            Reason = request.Reason,
            Status = AIStudyHub.Data.Enums.ReportStatus.Pending
        };

        await _unitOfWork.Reports.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Circuit Breaker: count distinct users who reported this document (excl. rejected)
        var uniqueVoters = await _unitOfWork.Reports.Query()
            .Where(r => r.DocumentId == request.DocumentId && r.Status != AIStudyHub.Data.Enums.ReportStatus.Rejected)
            .Select(r => r.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        if (uniqueVoters >= 5 && document.Status != AIStudyHub.Data.Enums.DocumentStatus.Banned)
        {
            document.Status = AIStudyHub.Data.Enums.DocumentStatus.Banned;
            _unitOfWork.Documents.Update(document);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Document {DocumentId} auto-banned after {Count} unique reports.", request.DocumentId, uniqueVoters);

            // Notify owner via real-time
            if (_realTimeNotifier != null)
            {
                try
                {
                    await _realTimeNotifier.SendNotificationAsync(new AIStudyHub.Business.DTOs.Notifications.RealTimeNotification(
                        document.UserId,
                        "Document auto-banned",
                        $"Your document \"{document.Title}\" has been automatically banned due to 5+ reports from distinct users.",
                        AIStudyHub.Data.Enums.NotificationType.Document,
                        DateTime.UtcNow,
                        new AIStudyHub.Business.DTOs.Notifications.ReportUpdatedPayload(report.Id, document.Id, AIStudyHub.Data.Enums.ReportStatus.Pending)
                    ), cancellationToken);
                }
                catch { /* best-effort */ }
            }
        }

        var created = await _unitOfWork.Reports.Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .Include(r => r.ResolvedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == report.Id, cancellationToken);

        return _mapper.Map<ReportResponseDto>(created);
    }

    public async Task<IReadOnlyList<ReportResponseDto>> GetMyReportsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var reports = await _unitOfWork.Reports.Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .Include(r => r.ResolvedByUser)
            .Where(r => r.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reports.Select(_mapper.Map<ReportResponseDto>).ToList();
    }

    public async Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<ReportResponseDto>> SearchAsync(ReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Reports.Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .Include(r => r.ResolvedByUser)
            .AsNoTracking();

        if (filter.Status.HasValue)
        {
            var statusEntity = (AIStudyHub.Data.Enums.ReportStatus)filter.Status.Value;
            query = query.Where(r => r.Status == statusEntity);
        }

        if (filter.DocumentId.HasValue)
        {
            query = query.Where(r => r.DocumentId == filter.DocumentId.Value);
        }

        if (filter.UserId.HasValue)
        {
            query = query.Where(r => r.UserId == filter.UserId.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt <= filter.ToDate.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new AIStudyHub.Business.DTOs.Common.PagedResultDto<ReportResponseDto>
        {
            Items = items.Select(_mapper.Map<ReportResponseDto>).ToList(),
            TotalCount = total,
            Offset = (filter.Page - 1) * filter.PageSize,
            Limit = filter.PageSize
        };
    }

    public async Task<ReportResponseDto> UpdateStatusAsync(Guid id, ReportStatusDto status, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports.Query()
            .Include(r => r.Document)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            throw new KeyNotFoundException($"Report with ID {id} not found.");
        }

        var newStatus = (AIStudyHub.Data.Enums.ReportStatus)status;

        // Optimistic concurrency check (since it's not a real RowVersion, we simulate by ensuring state is valid)
        if (report.Status == AIStudyHub.Data.Enums.ReportStatus.Resolved || report.Status == AIStudyHub.Data.Enums.ReportStatus.Rejected)
        {
            throw new InvalidOperationException("Cannot update a report that is already Resolved or Rejected.");
        }
        
        if (newStatus == AIStudyHub.Data.Enums.ReportStatus.Pending)
        {
            throw new InvalidOperationException("Cannot transition back to Pending.");
        }
        if (newStatus == AIStudyHub.Data.Enums.ReportStatus.Resolved || newStatus == AIStudyHub.Data.Enums.ReportStatus.Rejected)
        {
            if (report.Status != AIStudyHub.Data.Enums.ReportStatus.Reviewed)
            {
                throw new InvalidOperationException("Must transition to Reviewed before Resolved/Rejected.");
            }
        }

        report.Status = newStatus;
        report.ResolvedBy = adminUserId;
        report.ResolvedAt = DateTime.UtcNow;

        _unitOfWork.Reports.Update(report);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Real-time push to the report's reporter (no DB row per pure-SignalR design).
        if (_realTimeNotifier is not null)
        {
            try
            {
                var documentTitle = report.Document?.Title ?? "Document";
                await _realTimeNotifier.SendNotificationAsync(new RealTimeNotification(
                    report.UserId,
                    "Report updated",
                    $"Your report for \"{documentTitle}\" has been updated to {newStatus}.",
                    NotificationType.System,
                    DateTime.UtcNow,
                    new ReportUpdatedPayload(report.Id, report.DocumentId, newStatus)),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Report-updated real-time notify failed for report {ReportId}", report.Id);
            }
        }

        var updated = await _unitOfWork.Reports.Query()
            .Include(r => r.User)
            .Include(r => r.Document)
            .Include(r => r.ResolvedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return _mapper.Map<ReportResponseDto>(updated);
    }

    public async Task<int> MarkDocumentNonFlaggableAsync(Guid documentId, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        }

        if (document.IsNonFlaggable) return 0; // Already marked

        document.IsNonFlaggable = true;
        _unitOfWork.Documents.Update(document);

        // Reject all pending/reviewed reports
        var pendingReports = await _unitOfWork.Reports.Query()
            .Where(r => r.DocumentId == documentId && (r.Status == AIStudyHub.Data.Enums.ReportStatus.Pending || r.Status == AIStudyHub.Data.Enums.ReportStatus.Reviewed))
            .ToListAsync(cancellationToken);

        foreach (var report in pendingReports)
        {
            report.Status = AIStudyHub.Data.Enums.ReportStatus.Rejected;
            report.ResolvedBy = adminUserId;
            report.ResolvedAt = DateTime.UtcNow;
            _unitOfWork.Reports.Update(report);
        }

        // Add Notification deduplicated by Reporter (each user gets 1 notification)
        var userIdsToNotify = pendingReports.Select(r => r.UserId).Distinct().ToList();
        foreach (var userId in userIdsToNotify)
        {
            await _unitOfWork.Notifications.AddAsync(new Data.Entities.Notification
            {
                UserId = userId,
                Message = "Your report(s) were rejected because the document was verified as legitimate.",
                IsRead = false
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Real-time push to each affected reporter.
        if (_realTimeNotifier is not null)
        {
            var documentTitle = document.Title ?? "Document";
            foreach (var userId in userIdsToNotify)
            {
                try
                {
                    await _realTimeNotifier.SendNotificationAsync(new RealTimeNotification(
                        userId,
                        "Report rejected",
                        $"Your report for \"{documentTitle}\" was rejected. The document was verified as legitimate.",
                        NotificationType.System,
                        DateTime.UtcNow,
                        new ReportRejectedPayload(
                            pendingReports.Where(r => r.UserId == userId).Select(r => r.Id).ToList(),
                            documentId)),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Report-rejected real-time notify failed for user {UserId}", userId);
                }
            }
        }

        return pendingReports.Count;
    }

    public async Task<BulkReportStatusResultDto> BulkUpdateStatusAsync(IReadOnlyList<Guid> ids, ReportStatusDto status, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var newStatus = (AIStudyHub.Data.Enums.ReportStatus)status;
        var reports = await _unitOfWork.Reports.Query()
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        int updated = 0;
        var failed = new List<BulkFailureDto>();
        var userIdsToNotify = new HashSet<Guid>();

        foreach (var report in reports)
        {
            if (report.Status == AIStudyHub.Data.Enums.ReportStatus.Resolved || report.Status == AIStudyHub.Data.Enums.ReportStatus.Rejected)
            {
                failed.Add(new BulkFailureDto(report.Id, "Already resolved/rejected."));
                continue;
            }
            if (newStatus == AIStudyHub.Data.Enums.ReportStatus.Pending)
            {
                failed.Add(new BulkFailureDto(report.Id, "Cannot revert to Pending."));
                continue;
            }
            if ((newStatus == AIStudyHub.Data.Enums.ReportStatus.Resolved || newStatus == AIStudyHub.Data.Enums.ReportStatus.Rejected) && report.Status != AIStudyHub.Data.Enums.ReportStatus.Reviewed)
            {
                failed.Add(new BulkFailureDto(report.Id, "Must be Reviewed first."));
                continue;
            }

            report.Status = newStatus;
            report.ResolvedBy = adminUserId;
            report.ResolvedAt = DateTime.UtcNow;
            _unitOfWork.Reports.Update(report);
            userIdsToNotify.Add(report.UserId);
            updated++;
        }

        foreach (var userId in userIdsToNotify)
        {
            await _unitOfWork.Notifications.AddAsync(new Data.Entities.Notification
            {
                UserId = userId,
                Message = $"One or more of your reports have been updated to {newStatus}.",
                IsRead = false
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Real-time push to all affected reporters.
        if (_realTimeNotifier is not null)
        {
            foreach (var userId in userIdsToNotify)
            {
                try
                {
                    await _realTimeNotifier.SendNotificationAsync(new RealTimeNotification(
                        userId,
                        "Bulk report update",
                        $"One or more of your reports have been updated to {newStatus}.",
                        NotificationType.System,
                        DateTime.UtcNow,
                        null),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Bulk-report-updated real-time notify failed for user {UserId}", userId);
                }
            }
        }

        return new BulkReportStatusResultDto(updated, failed);
    }

    public async Task<BulkMarkNonFlaggableResultDto> BulkMarkNonFlaggableAsync(IReadOnlyList<Guid> documentIds, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        int totalDocuments = 0;
        int totalReportsRejected = 0;
        var affectedUsers = new HashSet<Guid>();

        foreach (var docId in documentIds)
        {
            var document = await _unitOfWork.Documents.GetByIdAsync(docId, cancellationToken);
            if (document != null && !document.IsNonFlaggable)
            {
                document.IsNonFlaggable = true;
                _unitOfWork.Documents.Update(document);
                totalDocuments++;

                var pendingReports = await _unitOfWork.Reports.Query()
                    .Where(r => r.DocumentId == docId && (r.Status == AIStudyHub.Data.Enums.ReportStatus.Pending || r.Status == AIStudyHub.Data.Enums.ReportStatus.Reviewed))
                    .ToListAsync(cancellationToken);

                var distinctUsers = new HashSet<Guid>();
                foreach (var report in pendingReports)
                {
                    report.Status = AIStudyHub.Data.Enums.ReportStatus.Rejected;
                    report.ResolvedBy = adminUserId;
                    report.ResolvedAt = DateTime.UtcNow;
                    _unitOfWork.Reports.Update(report);
                    distinctUsers.Add(report.UserId);
                    affectedUsers.Add(report.UserId);
                    totalReportsRejected++;
                }

                foreach (var userId in distinctUsers)
                {
                    await _unitOfWork.Notifications.AddAsync(new Data.Entities.Notification
                    {
                        UserId = userId,
                        Message = "Your report(s) were rejected because the document was verified as legitimate.",
                        IsRead = false
                    }, cancellationToken);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Real-time push to affected reporters.
        if (_realTimeNotifier is not null)
        {
            foreach (var userId in affectedUsers)
            {
                try
                {
                    await _realTimeNotifier.SendNotificationAsync(new RealTimeNotification(
                        userId,
                        "Reports rejected",
                        "One or more of your reports were rejected. Documents were verified as legitimate.",
                        NotificationType.System,
                        DateTime.UtcNow,
                        null),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Bulk-report-rejected real-time notify failed for user {UserId}", userId);
                }
            }
        }

        return new BulkMarkNonFlaggableResultDto(totalDocuments, totalReportsRejected);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            throw new KeyNotFoundException($"Report with ID {id} not found.");
        }

        if (report.Status == AIStudyHub.Data.Enums.ReportStatus.Pending)
        {
            throw new InvalidOperationException("Cannot delete a report that is Pending (Audit Trail intact).");
        }

        _unitOfWork.Reports.Remove(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class FlashcardService : IFlashcardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public FlashcardService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<FlashcardResponseDto>> GetAllPagedAsync(AIStudyHub.Business.DTOs.Common.PaginationParams @params, Guid userId, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Flashcards.Query()
            .Include(f => f.FlashcardDeck).ThenInclude(d => d.Document)
            .Where(f => f.FlashcardDeck.Document.UserId == userId || f.FlashcardDeck.Document.ShareStatus == "public")
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
        {
            var search = @params.SearchTerm.ToLower();
            query = query.Where(f => f.Front.ToLower().Contains(search) || f.Back.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(@params.SortBy))
        {
            query = @params.IsDescending 
                ? query.OrderByDescending(f => EF.Property<object>(f, @params.SortBy))
                : query.OrderBy(f => EF.Property<object>(f, @params.SortBy));
        }
        else
        {
            query = query.OrderByDescending(f => f.CreatedAt);
        }

        var items = await query.Skip(@params.Offset).Take(@params.Limit).ToListAsync(cancellationToken);

        var dtos = items.Select(_mapper.Map<FlashcardResponseDto>).ToList();
        return new AIStudyHub.Business.DTOs.Common.PagedResultDto<FlashcardResponseDto>(dtos, totalCount, @params.Offset, @params.Limit);
    }

    public async Task<IReadOnlyList<FlashcardResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var flashcards = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.FlashcardDeck).ThenInclude(d => d.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return flashcards.Select(_mapper.Map<FlashcardResponseDto>).ToList();
    }

    public async Task<FlashcardResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var flashcard = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.FlashcardDeck).ThenInclude(d => d.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return flashcard is null ? null : _mapper.Map<FlashcardResponseDto>(flashcard);
    }

    public async Task<FlashcardResponseDto> CreateAsync(CreateFlashcardRequestDto request, CancellationToken cancellationToken = default)
    {
        var deckExists = await _unitOfWork.FlashcardDecks.GetByIdAsync(request.DeckId, cancellationToken) is not null;
        if (!deckExists)
        {
            throw new KeyNotFoundException($"FlashcardDeck with ID {request.DeckId} not found.");
        }

        var flashcard = _mapper.Map<Data.Entities.Flashcard>(request);
        await _unitOfWork.Flashcards.AddAsync(flashcard, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.FlashcardDeck).ThenInclude(d => d.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == flashcard.Id, cancellationToken);

        return _mapper.Map<FlashcardResponseDto>(created);
    }

    public async Task<FlashcardResponseDto> UpdateAsync(Guid id, UpdateFlashcardRequestDto request, CancellationToken cancellationToken = default)
    {
        var flashcard = await _unitOfWork.Flashcards.GetByIdAsync(id, cancellationToken);
        if (flashcard is null)
        {
            throw new KeyNotFoundException($"Flashcard with ID {id} not found.");
        }

        _mapper.Map(request, flashcard);
        _unitOfWork.Flashcards.Update(flashcard);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.FlashcardDeck).ThenInclude(d => d.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return _mapper.Map<FlashcardResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var flashcard = await _unitOfWork.Flashcards.GetByIdAsync(id, cancellationToken);
        if (flashcard is null)
        {
            throw new KeyNotFoundException($"Flashcard with ID {id} not found.");
        }

        _unitOfWork.Flashcards.Remove(flashcard);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteDeckAsync(
        Guid deckId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ownedDeckExists = await _unitOfWork.FlashcardDecks
            .Query()
            .AsNoTracking()
            .AnyAsync(
                deck => deck.Id == deckId && deck.Document.UserId == userId,
                cancellationToken);
        if (!ownedDeckExists)
            throw new KeyNotFoundException("Deck not found.");

        var flashcards = await _unitOfWork.Flashcards
            .Query()
            .Where(flashcard => flashcard.DeckId == deckId)
            .ToListAsync(cancellationToken);

        foreach (var flashcard in flashcards)
            _unitOfWork.Flashcards.Remove(flashcard);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return flashcards.Count;
    }

    public async Task<int> DeleteByDocumentAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var documentExists = await _unitOfWork.Documents
            .Query()
            .AsNoTracking()
            .AnyAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);
        if (!documentExists)
            throw new KeyNotFoundException("Document not found.");

        var decks = await _unitOfWork.FlashcardDecks
            .Query()
            .Where(deck => deck.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        var deckIds = decks.Select(d => d.Id).ToList();
        var flashcards = await _unitOfWork.Flashcards
            .Query()
            .Where(f => deckIds.Contains(f.DeckId))
            .ToListAsync(cancellationToken);

        foreach (var flashcard in flashcards)
            _unitOfWork.Flashcards.Remove(flashcard);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return flashcards.Count;
    }

    public async Task<IReadOnlyList<FlashcardResponseDto>> GetByDeckAsync(
        Guid deckId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var flashcards = await _unitOfWork.Flashcards
            .Query()
            .Include(f => f.FlashcardDeck).ThenInclude(d => d.Document)
            .Where(f => f.DeckId == deckId && (f.FlashcardDeck.Document.UserId == userId || f.FlashcardDeck.Document.ShareStatus == "public"))
            .OrderBy(f => f.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return flashcards.Select(_mapper.Map<FlashcardResponseDto>).ToList();
    }

    public async Task<IReadOnlyList<FlashcardDeckSummaryDto>> GetDecksByDocumentAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var decks = await _unitOfWork.FlashcardDecks
            .Query()
            .AsNoTracking()
            .Where(d => d.DocumentId == documentId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        if (decks.Count == 0)
            return Array.Empty<FlashcardDeckSummaryDto>();

        var document = await _unitOfWork.Documents
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
            throw new KeyNotFoundException("Document not found.");

        if (document.UserId != userId && document.ShareStatus != "public")
            throw new KeyNotFoundException("Document not found.");

        var deckIds = decks.Select(d => d.Id).ToList();
        var counts = await _unitOfWork.Flashcards
            .Query()
            .AsNoTracking()
            .Where(f => deckIds.Contains(f.DeckId))
            .GroupBy(f => f.DeckId)
            .Select(g => new { DeckId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeckId, x => x.Count, cancellationToken);

        return decks.Select(d => new FlashcardDeckSummaryDto(
            d.Id,
            d.DocumentId,
            d.Name,
            d.CreatedAt,
            counts.TryGetValue(d.Id, out var c) ? c : 0
        )).ToList();
    }

    public async Task<IReadOnlyList<FlashcardResponseDto>> CreateBulkAsync(
        IReadOnlyList<CreateFlashcardRequestDto> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests is null || requests.Count == 0)
            return Array.Empty<FlashcardResponseDto>();

        var deckIds = requests.Select(r => r.DeckId).Distinct().ToList();
        var allDecksExist = await _unitOfWork.FlashcardDecks
            .Query()
            .Where(d => deckIds.Contains(d.Id))
            .Select(d => d.Id)
            .CountAsync(cancellationToken) == deckIds.Count;

        if (!allDecksExist)
            throw new KeyNotFoundException("One or more decks not found.");

        var flashcards = requests
            .Select(r => _mapper.Map<Data.Entities.Flashcard>(r))
            .ToList();

        foreach (var flashcard in flashcards)
            await _unitOfWork.Flashcards.AddAsync(flashcard, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var ids = flashcards.Select(f => f.Id).ToList();
        var saved = await _unitOfWork.Flashcards
            .Query()
            .Where(f => ids.Contains(f.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return saved.Select(_mapper.Map<FlashcardResponseDto>).ToList();
    }
}

public sealed class QuizService : IQuizService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public QuizService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<AIStudyHub.Business.DTOs.Common.PagedResultDto<QuizResponseDto>> GetAllPagedAsync(AIStudyHub.Business.DTOs.Common.PaginationParams @params, Guid userId, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Quizzes.Query()
            .Include(q => q.Document)
            .Where(q => q.Document.UserId == userId || q.Document.ShareStatus == "public")
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
        {
            var search = @params.SearchTerm.ToLower();
            query = query.Where(q => q.Title.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(@params.SortBy))
        {
            query = @params.IsDescending 
                ? query.OrderByDescending(q => EF.Property<object>(q, @params.SortBy))
                : query.OrderBy(q => EF.Property<object>(q, @params.SortBy));
        }
        else
        {
            query = query.OrderByDescending(q => q.CreatedAt);
        }

        var items = await query.Skip(@params.Offset).Take(@params.Limit).ToListAsync(cancellationToken);

        var dtos = items.Select(_mapper.Map<QuizResponseDto>).ToList();
        return new AIStudyHub.Business.DTOs.Common.PagedResultDto<QuizResponseDto>(dtos, totalCount, @params.Offset, @params.Limit);
    }

    public async Task<IReadOnlyList<QuizResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var quizzes = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return quizzes.Select(_mapper.Map<QuizResponseDto>).ToList();
    }

    public async Task<QuizResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .Include(q => q.Questions)
                .ThenInclude(q => q.Answers)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return quiz is null ? null : _mapper.Map<QuizResponseDto>(quiz);
    }

    public async Task<QuizResponseDto> CreateAsync(CreateQuizRequestDto request, CancellationToken cancellationToken = default)
    {
        var documentExists = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId, cancellationToken) is not null;
        if (!documentExists)
        {
            throw new KeyNotFoundException($"Document with ID {request.DocumentId} not found.");
        }

        var quiz = _mapper.Map<Data.Entities.Quiz>(request);
        await _unitOfWork.Quizzes.AddAsync(quiz, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quiz.Id, cancellationToken);

        return _mapper.Map<QuizResponseDto>(created);
    }

    public async Task<QuizResponseDto> UpdateAsync(Guid id, UpdateQuizRequestDto request, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes.GetByIdAsync(id, cancellationToken);
        if (quiz is null)
        {
            throw new KeyNotFoundException($"Quiz with ID {id} not found.");
        }

        _mapper.Map(request, quiz);
        _unitOfWork.Quizzes.Update(quiz);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return _mapper.Map<QuizResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes.GetByIdAsync(id, cancellationToken);
        if (quiz is null)
        {
            throw new KeyNotFoundException($"Quiz with ID {id} not found.");
        }

        _unitOfWork.Quizzes.Remove(quiz);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class QuestionService : IQuestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    public QuestionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<QuestionResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var questions = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return questions.Select(_mapper.Map<QuestionResponseDto>).ToList();
    }

    public async Task<QuestionResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var question = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return question is null ? null : _mapper.Map<QuestionResponseDto>(question);
    }

    public async Task<IReadOnlyList<QuestionResponseDto>> GetByQuizIdAsync(Guid quizId, CancellationToken cancellationToken = default)
    {
        var questions = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .Include(q => q.Answers)
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.Position)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return questions.Select(_mapper.Map<QuestionResponseDto>).ToList();
    }

    public async Task<QuestionResponseDto> CreateAsync(CreateQuestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var quizExists = await _unitOfWork.Quizzes.GetByIdAsync(request.QuizId, cancellationToken) is not null;
        if (!quizExists)
        {
            throw new KeyNotFoundException($"Quiz with ID {request.QuizId} not found.");
        }

        var question = _mapper.Map<Data.Entities.Question>(request);
        await _unitOfWork.Questions.AddAsync(question, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == question.Id, cancellationToken);

        return _mapper.Map<QuestionResponseDto>(created);
    }

    public async Task<QuestionResponseDto> UpdateAsync(Guid id, UpdateQuestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var question = await _unitOfWork.Questions.GetByIdAsync(id, cancellationToken);
        if (question is null)
        {
            throw new KeyNotFoundException($"Question with ID {id} not found.");
        }

        _mapper.Map(request, question);
        _unitOfWork.Questions.Update(question);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Questions
            .Query()
            .Include(q => q.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return _mapper.Map<QuestionResponseDto>(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var question = await _unitOfWork.Questions.GetByIdAsync(id, cancellationToken);
        if (question is null)
        {
            throw new KeyNotFoundException($"Question with ID {id} not found.");
        }

        _unitOfWork.Questions.Remove(question);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AnswerService : IAnswerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public AnswerService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AnswerResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var answers = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return answers.Select(_mapper.Map<AnswerResponseDto>).ToList();
    }

    public async Task<AnswerResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var answer = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return answer is null ? null : _mapper.Map<AnswerResponseDto>(answer);
    }

    public async Task<IReadOnlyList<AnswerResponseDto>> GetByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var answers = await _unitOfWork.Answers
            .Query()
            .Include(a => a.Question)
            .Where(a => a.QuestionId == questionId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return answers.Select(_mapper.Map<AnswerResponseDto>).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var answer = await _unitOfWork.Answers.GetByIdAsync(id, cancellationToken);
        if (answer is null)
        {
            throw new KeyNotFoundException($"Answer with ID {id} not found.");
        }

        _unitOfWork.Answers.Remove(answer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class QuizSubmissionService : IQuizSubmissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly AIStudyHub.Business.Interfaces.Services.IGamificationService? _gamificationService;
    private readonly AIStudyHub.Business.Interfaces.Services.IBadgeService? _badgeService;
    private readonly AIStudyHub.Business.Interfaces.Services.IRecommendationService? _recommendationService;
    private readonly IRealTimeNotificationService? _realTimeNotifier;
    private readonly ILogger<QuizSubmissionService>? _logger;

    public QuizSubmissionService(IUnitOfWork unitOfWork, IMapper mapper,
        AIStudyHub.Business.Interfaces.Services.IGamificationService? gamificationService = null,
        AIStudyHub.Business.Interfaces.Services.IBadgeService? badgeService = null,
        AIStudyHub.Business.Interfaces.Services.IRecommendationService? recommendationService = null,
        IRealTimeNotificationService? realTimeNotifier = null,
        ILogger<QuizSubmissionService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _gamificationService = gamificationService;
        _badgeService = badgeService;
        _recommendationService = recommendationService;
        _realTimeNotifier = realTimeNotifier;
        _logger = logger;
    }

    public async Task<IReadOnlyList<QuizSubmissionResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var submissions = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return submissions.Select(_mapper.Map<QuizSubmissionResponseDto>).ToList();
    }

    public async Task<QuizSubmissionDetailDto?> GetOwnedDetailAsync(
        Guid submissionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var submission = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.Quiz)
                .ThenInclude(quiz => quiz.Document)
                    .ThenInclude(document => document.Subject)
            .Include(qs => qs.Quiz)
                .ThenInclude(quiz => quiz.Questions)
                    .ThenInclude(question => question.Answers)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                qs => qs.Id == submissionId && qs.UserId == userId,
                cancellationToken);

        if (submission is null)
            return null;

        var submittedAnswers = DeserializeSubmittedAnswers(
            submission.Answers,
            submission.Id);
        var questions = submission.Quiz.Questions
            .OrderBy(question => question.Position)
            .ThenBy(question => question.Id)
            .Select(question =>
            {
                submittedAnswers.TryGetValue(
                    question.Id.ToString(),
                    out var selectedOption);
                var options = question.Answers
                    .OrderBy(answer => answer.CreatedAt)
                    .ThenBy(answer => answer.Id)
                    .Select(answer => new QuizSubmissionOptionDetailDto(
                        answer.Id,
                        answer.SelectedOption,
                        selectedOption is not null
                            && AnswersMatch(answer.SelectedOption, selectedOption),
                        answer.IsCorrect))
                    .ToList();

                return new QuizSubmissionQuestionDetailDto(
                    question.Id,
                    question.Title,
                    question.Type,
                    question.Position,
                    options);
            })
            .ToList();

        return new QuizSubmissionDetailDto(
            submission.Id,
            submission.QuizId,
            submission.Quiz.Title,
            submission.Quiz.DocumentId,
            submission.Quiz.Document.Title,
            submission.Quiz.Document.SubjectId,
            submission.Quiz.Document.Subject.SubjectCode,
            submission.Quiz.Document.Subject.SubjectName,
            submission.Score,
            submission.MaxScore,
            submission.TotalCorrect,
            submission.DurationSeconds,
            submission.MaxScore > 0
                ? Math.Round((double)submission.Score / submission.MaxScore * 100, 1)
                : 0,
            submission.GradedAt,
            submission.SubmittedAt,
            questions);
    }

    public async Task<IReadOnlyList<QuizSubmissionResponseDto>> GetByUserAndQuizAsync(Guid userId, Guid quizId, CancellationToken cancellationToken = default)
    {
        var submissions = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .Where(qs => qs.UserId == userId && qs.QuizId == quizId)
            .OrderByDescending(qs => qs.SubmittedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return submissions.Select(_mapper.Map<QuizSubmissionResponseDto>).ToList();
    }

    public async Task<PagedResultDto<QuizSubmissionHistoryDto>> GetMyHistoryAsync(
        Guid userId,
        Guid? quizId,
        DateTime? fromDate,
        DateTime? toDate,
        PaginationParams @params,
        CancellationToken ct = default)
    {
        var query = _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.Quiz)
                .ThenInclude(q => q.Document)
                    .ThenInclude(d => d.Subject)
            .Where(qs => qs.UserId == userId);

        if (quizId.HasValue)
            query = query.Where(qs => qs.QuizId == quizId.Value);

        if (fromDate.HasValue)
            query = query.Where(qs => qs.SubmittedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(qs => qs.SubmittedAt <= toDate.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(qs => qs.SubmittedAt)
            .Skip(@params.Offset)
            .Take(@params.Limit)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = items.Select(qs => new QuizSubmissionHistoryDto(
            qs.Id, qs.UserId, qs.QuizId,
            qs.Quiz?.Title ?? string.Empty,
            qs.Quiz?.Document?.Title ?? string.Empty,
            qs.Quiz?.Document?.Subject?.SubjectCode ?? string.Empty,
            qs.Score, qs.MaxScore, qs.TotalCorrect,
            qs.DurationSeconds,
            qs.MaxScore > 0 ? Math.Round((double)qs.Score / qs.MaxScore * 100, 1) : 0,
            qs.GradedAt, qs.SubmittedAt, qs.CreatedAt, qs.UpdatedAt)).ToList();

        return new PagedResultDto<QuizSubmissionHistoryDto>(dtos, totalCount, @params.Offset, @params.Limit);
    }

    public async Task<PagedResultDto<QuizSubmissionHistoryDto>> GetQuizHistoryAsync(
        Guid quizId,
        Guid userId,
        DateTime? fromDate,
        DateTime? toDate,
        PaginationParams @params,
        CancellationToken ct = default)
    {
        var query = _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.Quiz)
                .ThenInclude(q => q.Document)
                    .ThenInclude(d => d.Subject)
            .Where(qs => qs.UserId == userId && qs.QuizId == quizId);

        if (fromDate.HasValue)
            query = query.Where(qs => qs.SubmittedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(qs => qs.SubmittedAt <= toDate.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(qs => qs.SubmittedAt)
            .Skip(@params.Offset)
            .Take(@params.Limit)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = items.Select(qs => new QuizSubmissionHistoryDto(
            qs.Id, qs.UserId, qs.QuizId,
            qs.Quiz?.Title ?? string.Empty,
            qs.Quiz?.Document?.Title ?? string.Empty,
            qs.Quiz?.Document?.Subject?.SubjectCode ?? string.Empty,
            qs.Score, qs.MaxScore, qs.TotalCorrect,
            qs.DurationSeconds,
            qs.MaxScore > 0 ? Math.Round((double)qs.Score / qs.MaxScore * 100, 1) : 0,
            qs.GradedAt, qs.SubmittedAt, qs.CreatedAt, qs.UpdatedAt)).ToList();

        return new PagedResultDto<QuizSubmissionHistoryDto>(dtos, totalCount, @params.Offset, @params.Limit);
    }

    public async Task<QuizSubmissionResponseDto> CreateAsync(CreateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default)
    {
        var quiz = await _unitOfWork.Quizzes
            .Query()
            .Include(q => q.Questions)
                .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == request.QuizId, cancellationToken);

        if (quiz is null)
        {
            throw new KeyNotFoundException($"Quiz with ID {request.QuizId} not found.");
        }

        var submission = new Data.Entities.QuizSubmission
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            QuizId = request.QuizId,
            Answers = request.Answers,
            DurationSeconds = request.DurationSeconds,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        // Grade the submission
        if (quiz.Questions.Any())
        {
            var maxScore = quiz.Questions.Count;
            var totalCorrect = 0;

            // Simple grading: parse submitted answers and match with questions
            // Assuming request.Answers is JSON string like "{\"q1\":\"A\",\"q2\":\"B\"}"
            // And Question has Answers where IsCorrect == true
            var submittedAnswers = DeserializeSubmittedAnswers(
                request.Answers,
                submission.Id);

            foreach (var question in quiz.Questions)
            {
                var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
                if (correctAnswer != null && submittedAnswers.TryGetValue(question.Id.ToString(), out var selectedOption))
                {
                    if (AnswersMatch(correctAnswer.SelectedOption, selectedOption))
                    {
                        totalCorrect++;
                    }
                }
            }

            submission.Score = totalCorrect;
            submission.MaxScore = maxScore;
            submission.TotalCorrect = totalCorrect;
            submission.GradedAt = DateTime.UtcNow;

            // Phase 4b: create WeakSubject recommendation if mastery drops below 60% for this subject
            if (_recommendationService != null && quiz.Document != null)
            {
                try
                {
                    var subjectCode = quiz.Document.Subject?.SubjectCode ?? string.Empty;
                    var subjectName = quiz.Document.Subject?.SubjectName ?? string.Empty;
                    var subjectId = quiz.Document.SubjectId;

                    var subjectTotal = await _unitOfWork.StudyLogs
                        .Query()
                        .CountAsync(l => l.UserId == request.UserId && l.SubjectCode == subjectCode, cancellationToken);
                    var subjectCorrect = await _unitOfWork.StudyLogs
                        .Query()
                        .CountAsync(l => l.UserId == request.UserId && l.SubjectCode == subjectCode && l.IsCorrect, cancellationToken);
                    var overallTotal = subjectTotal + maxScore;
                    var overallCorrect = subjectCorrect + totalCorrect;
                    var mastery = overallTotal > 0 ? Math.Round((double)overallCorrect / overallTotal * 100, 1) : 0.0;

                    if (mastery < 60)
                    {
                        await _recommendationService.CreateWeakSubjectRecommendationAsync(
                            request.UserId, subjectId, subjectName, subjectCode, mastery, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to create weak-subject recommendation for user {UserId}", request.UserId);
                }
            }
        }

        await _unitOfWork.QuizSubmissions.AddAsync(submission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.QuizSubmissions
            .Query()
            .Include(qs => qs.User)
            .Include(qs => qs.Quiz)
            .AsNoTracking()
            .FirstOrDefaultAsync(qs => qs.Id == submission.Id, cancellationToken);

        return _mapper.Map<QuizSubmissionResponseDto>(created);
    }

    /// <summary>
    /// Plan C3 / B.4.5 — submit a quiz attempt and return the saved submission plus
    /// any badges the user just unlocked (Sharpshooter, Math Prodigy).
    /// Prefer this over <see cref="CreateAsync"/> for the user-facing endpoint.
    /// </summary>
    public async Task<SubmitQuizResultDto> SubmitAsync(CreateQuizSubmissionRequestDto request, CancellationToken cancellationToken = default)
    {
        var submission = await CreateAsync(request, cancellationToken);

        // Plan C4 / Spec v4.0: award XP here (moved out of CreateAsync) and surface
        // the actual XpEarned in the response so the UI can celebrate without an
        // extra round-trip to /api/Gamification/stats.
        int xpEarned = 0;
        if (_gamificationService is not null && submission.MaxScore > 0)
        {
            try
            {
                var quiz = await _unitOfWork.Quizzes.GetByIdAsync(submission.QuizId, cancellationToken);
                if (quiz is not null)
                {
                    var documentId = quiz.DocumentId;
                    var subjectCode = await _unitOfWork.Documents.Query()
                        .Where(d => d.Id == documentId)
                        .Select(d => d.Subject.SubjectCode)
                        .FirstOrDefaultAsync(cancellationToken);

                    var allCorrect = submission.TotalCorrect == submission.MaxScore;
                    var xpResult = await _gamificationService.AwardXpAsync(
                        new AIStudyHub.Business.DTOs.Gamification.XpAwardRequest(
                            UserId: submission.UserId,
                            XpEarned: 0, // computed inside service
                            IsCorrect: allCorrect,
                            ActivityType: AIStudyHub.Data.Enums.ActivityType.QuizSubmission,
                            DocumentId: documentId,
                            SubjectCode: subjectCode,
                            TimeSpentSeconds: request.DurationSeconds),
                        cancellationToken);

                    if (xpResult is { Success: true, Data: not null })
                    {
                        xpEarned = xpResult.Data.XpEarned;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Gamification XP award failed for user {UserId}, submission {SubmissionId}", submission.UserId, submission.Id);
                // Swallow: quiz was already graded and saved.
            }
        }

        IReadOnlyList<AchievementDto> unlocked = Array.Empty<AchievementDto>();
        if (_badgeService is not null)
        {
            try
            {
                var entity = await _unitOfWork.QuizSubmissions.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == submission.Id, cancellationToken);

                if (entity is not null)
                {
                    unlocked = await _badgeService.EvaluateQuizBadgeAsync(submission.UserId, entity, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Badge evaluation failed for user {UserId}, submission {SubmissionId}", submission.UserId, submission.Id);
            }
        }

        // Real-time quiz-graded push (in addition to the synchronous HTTP response).
        if (_realTimeNotifier is not null && submission.MaxScore > 0)
        {
            try
            {
                var quiz = await _unitOfWork.Quizzes
                    .Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == submission.QuizId, cancellationToken);
                var quizTitle = quiz?.Title ?? "Quiz";

                await _realTimeNotifier.NotifyQuizGradedAsync(
                    submission.UserId,
                    submission.QuizId,
                    quizTitle,
                    submission.Score,
                    submission.MaxScore,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Quiz-graded real-time notify failed for submission {SubmissionId}", submission.Id);
            }
        }

        return new SubmitQuizResultDto(submission, xpEarned, unlocked);
    }

    private Dictionary<string, string> DeserializeSubmittedAnswers(
        string serializedAnswers,
        Guid submissionId)
    {
        try
        {
            var submittedAnswers = JsonSerializer.Deserialize<Dictionary<string, string>>(
                serializedAnswers);
            if (submittedAnswers is null
                || submittedAnswers.Any(answer =>
                    !Guid.TryParse(answer.Key, out var questionId)
                    || questionId == Guid.Empty
                    || string.IsNullOrWhiteSpace(answer.Value)))
            {
                throw new JsonException();
            }

            return submittedAnswers;
        }
        catch (JsonException)
        {
            _logger?.LogError(
                "Stored answers are invalid for quiz submission {SubmissionId}",
                submissionId);
            throw new CorruptedQuizSubmissionException(submissionId);
        }
    }

    private static bool AnswersMatch(string expected, string selected)
    {
        return string.Equals(expected, selected, StringComparison.OrdinalIgnoreCase);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var submission = await _unitOfWork.QuizSubmissions.GetByIdAsync(id, cancellationToken);
        if (submission is null)
        {
            throw new KeyNotFoundException($"Quiz submission with ID {id} not found.");
        }

        _unitOfWork.QuizSubmissions.Remove(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public NotificationService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<NotificationResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return notifications.Select(_mapper.Map<NotificationResponseDto>).ToList();
    }

    public async Task<NotificationResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications
            .Query()
            .Include(n => n.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        return notification is null ? null : _mapper.Map<NotificationResponseDto>(notification);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            throw new KeyNotFoundException($"Notification with ID {id} not found.");
        }

        _unitOfWork.Notifications.Remove(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResponseDto>> GetUserNotificationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _unitOfWork.Notifications
            .Query()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return notifications.Select(n => new NotificationResponseDto(
            n.Id, n.UserId, n.Title, n.Message, n.PayloadJson, n.ActionUrl, n.IsRead, n.Type.ToString(), n.CreatedAt, n.UpdatedAt)).ToList();
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            throw new KeyNotFoundException($"Notification with ID {notificationId} not found.");
        }

        notification.IsRead = true;
        _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _unitOfWork.Notifications
            .Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            _unitOfWork.Notifications.Update(notification);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Notifications
            .Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync(cancellationToken);
    }

    public async Task<int> GetUnreadSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        return await _unitOfWork.Notifications.Query()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }
}

public sealed class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IVnPayService _vnPayService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IRealTimeNotificationService? _realTimeNotifier;
    private readonly ILogger<PaymentService>? _logger;

    public PaymentService(IUnitOfWork unitOfWork, IMapper mapper, IVnPayService vnPayService,
        IHttpContextAccessor httpContextAccessor,
        IRealTimeNotificationService? realTimeNotifier = null,
        ILogger<PaymentService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _vnPayService = vnPayService;
        _httpContextAccessor = httpContextAccessor;
        _realTimeNotifier = realTimeNotifier;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PaymentResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var payments = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return payments.Select(_mapper.Map<PaymentResponseDto>).ToList();
    }

    public async Task<PaymentResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return payment is null ? null : _mapper.Map<PaymentResponseDto>(payment);
    }

    public async Task<PaymentLinkResponseDto> CreatePaymentUrlAsync(CreatePaymentLinkRequestDto request, CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        var userIdString = context?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            throw new UnauthorizedAccessException("User not authenticated or invalid ID.");
        }

        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(request.TierId, cancellationToken);
        if (tier is null)
        {
            throw new KeyNotFoundException($"Tier with ID {request.TierId} not found.");
        }

        if (tier.Price <= 0)
        {
            throw new InvalidOperationException($"Tier '{tier.TierName}' does not have a valid price configured.");
        }

        var payment = new Data.Entities.Payment
        {
            UserId = userId,
            TierId = request.TierId,
            Amount = tier.Price,
            Status = Data.Enums.PaymentStatus.Pending,
            PaymentInfo = $"Upgrade to {tier.TierName} tier",
            PaymentDate = DateTime.UtcNow
        };

        await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var clientIp = context?.Connection?.RemoteIpAddress?.ToString();
        var url = _vnPayService.CreatePaymentUrl(clientIp!, payment.Id, payment.Amount, payment.PaymentInfo);
        return new PaymentLinkResponseDto(url);
    }

    public async Task<IReadOnlyList<PaymentResponseDto>> GetUserPaymentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var payments = await _unitOfWork.Payments
            .Query()
            .Include(p => p.User)
            .Include(p => p.TierMembership)
            .Where(p => p.UserId == userId)
            .AsNoTracking()
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        return payments.Select(_mapper.Map<PaymentResponseDto>).ToList();
    }

    public async Task RefundPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            throw new KeyNotFoundException($"Payment with ID {paymentId} not found.");
        }

        if (payment.Status == Data.Enums.PaymentStatus.Refunded)
        {
            throw new InvalidOperationException("Payment has already been refunded.");
        }

        if (payment.Status != Data.Enums.PaymentStatus.Completed)
        {
            throw new InvalidOperationException("Only completed payments can be refunded.");
        }

        payment.Status = Data.Enums.PaymentStatus.Refunded;
        _unitOfWork.Payments.Update(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VnpayReturnResult> HandleVnpayReturnAsync(IQueryCollection query, CancellationToken cancellationToken = default)
    {
        if (!_vnPayService.ValidateSignature(query))
        {
            return new VnpayReturnResult { IsValidSignature = false };
        }

        var paymentIdString = query["vnp_TxnRef"].ToString();
        var responseCode = query["vnp_ResponseCode"].ToString();
        var transactionId = query["vnp_TransactionNo"].ToString();

        if (!Guid.TryParse(paymentIdString, out var paymentId))
        {
            return new VnpayReturnResult { Message = "Invalid payment ID" };
        }

        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return new VnpayReturnResult { Message = "Payment not found" };
        }

        if (payment.Status != Data.Enums.PaymentStatus.Pending)
        {
            return new VnpayReturnResult
            {
                IsSuccess = payment.Status == Data.Enums.PaymentStatus.Completed,
                Message = "Payment already processed",
                Status = payment.Status.ToString()
            };
        }

        payment.TransactionId = transactionId;

        if (responseCode == "00")
        {
            payment.Status = Data.Enums.PaymentStatus.Completed;
            _unitOfWork.Payments.Update(payment);

            var user = await _unitOfWork.Users.GetByIdAsync(payment.UserId, cancellationToken);
            string? tierName = null;
            DateTime? expiresAt = null;
            if (user is not null && payment.TierId.HasValue)
            {
                var tier = await _unitOfWork.TierMemberships.GetByIdAsync(payment.TierId.Value, cancellationToken);
                user.TierId = payment.TierId.Value;
                user.TierExpireAt = tier is not null && !tier.TierName.Equals("Free", StringComparison.OrdinalIgnoreCase)
                    ? DateTime.UtcNow.AddDays(30)
                    : null;
                _unitOfWork.Users.Update(user);

                tierName = tier?.TierName;
                expiresAt = user.TierExpireAt;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Real-time payment-succeeded push.
            if (_realTimeNotifier is not null && user is not null && tierName is not null)
            {
                try
                {
                    var activatedAt = DateTime.UtcNow;
                    var effectiveExpiry = expiresAt ?? activatedAt.AddDays(30);
                    await _realTimeNotifier.NotifyPaymentSucceededAsync(
                        user.Id, tierName, activatedAt, effectiveExpiry, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Payment-succeeded real-time notify failed for user {UserId}", payment.UserId);
                }
            }

            return new VnpayReturnResult
            {
                IsSuccess = true,
                Message = "Thanh toán thành công",
                Status = Data.Enums.PaymentStatus.Completed.ToString()
            };
        }

        payment.Status = Data.Enums.PaymentStatus.Failed;
        _unitOfWork.Payments.Update(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VnpayReturnResult
        {
            IsSuccess = false,
            Message = "Thanh toán thất bại hoặc bị hủy",
            Status = Data.Enums.PaymentStatus.Failed.ToString()
        };
    }
}

public sealed class SubjectService : ISubjectService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SubjectService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<SubjectResponseDto>> GetMineAsync(
        Guid ownerUserId,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Subjects.Query()
            .Where(subject => subject.OwnerUserId == ownerUserId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var search = pagination.SearchTerm.ToLower();
            query = query.Where(s => s.SubjectName.ToLower().Contains(search) || s.SubjectCode.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(pagination.SortBy))
        {
            query = pagination.IsDescending
                ? query.OrderByDescending(s => EF.Property<object>(s, pagination.SortBy))
                : query.OrderBy(s => EF.Property<object>(s, pagination.SortBy));
        }
        else
        {
            query = query.OrderByDescending(s => s.CreatedAt);
        }

        var items = await query.Skip(pagination.Offset).Take(pagination.Limit).ToListAsync(cancellationToken);

        var dtos = items.Select(_mapper.Map<SubjectResponseDto>).ToList();
        return new PagedResultDto<SubjectResponseDto>(dtos, totalCount, pagination.Offset, pagination.Limit);
    }

    public async Task<SubjectResponseDto?> GetOwnedByIdAsync(
        Guid ownerUserId,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var subject = await _unitOfWork.Subjects.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                subject => subject.Id == subjectId
                    && subject.OwnerUserId == ownerUserId,
                cancellationToken);

        return subject is null ? null : _mapper.Map<SubjectResponseDto>(subject);
    }

    public Task<bool> ExistsForOwnerAsync(
        Guid ownerUserId,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        return _unitOfWork.Subjects.Query()
            .AnyAsync(
                subject => subject.Id == subjectId
                    && subject.OwnerUserId == ownerUserId,
                cancellationToken);
    }

    public async Task<SubjectResponseDto> CreateForUserAsync(
        Guid ownerUserId,
        CreateSubjectRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = request.SubjectCode.Trim().ToUpperInvariant();
        var exists = await _unitOfWork.Subjects.Query()
            .AnyAsync(
                subject => subject.OwnerUserId == ownerUserId
                    && subject.SubjectCode == normalizedCode,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Subject with code '{normalizedCode}' already exists.");
        }

        var subject = _mapper.Map<Data.Entities.Subject>(request);
        subject.OwnerUserId = ownerUserId;
        subject.SubjectCode = normalizedCode;
        subject.SubjectName = request.SubjectName.Trim();
        await _unitOfWork.Subjects.AddAsync(subject, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SubjectResponseDto>(subject);
    }

    public async Task<SubjectResponseDto> UpdateOwnedAsync(
        Guid ownerUserId,
        Guid subjectId,
        UpdateSubjectRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = request.SubjectCode.Trim().ToUpperInvariant();
        var subject = await _unitOfWork.Subjects.Query()
            .FirstOrDefaultAsync(
                subject => subject.Id == subjectId
                    && subject.OwnerUserId == ownerUserId,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Subject with ID {subjectId} not found.");

        var codeConflict = await _unitOfWork.Subjects.Query()
            .AnyAsync(
                candidate => candidate.OwnerUserId == ownerUserId
                    && candidate.SubjectCode == normalizedCode
                    && candidate.Id != subjectId,
                cancellationToken);

        if (codeConflict)
        {
            throw new InvalidOperationException($"Subject with code '{normalizedCode}' already exists.");
        }

        subject.SubjectCode = normalizedCode;
        subject.SubjectName = request.SubjectName.Trim();
        subject.Description = request.Description;
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SubjectResponseDto>(subject);
    }

    public async Task DeleteOwnedAsync(
        Guid ownerUserId,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var subject = await _unitOfWork.Subjects.Query()
            .FirstOrDefaultAsync(
                subject => subject.Id == subjectId
                    && subject.OwnerUserId == ownerUserId,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Subject with ID {subjectId} not found.");

        var isReferenced = await _unitOfWork.Documents.Query()
            .AnyAsync(document => document.SubjectId == subjectId, cancellationToken);

        if (isReferenced)
        {
            throw new InvalidOperationException(
                "Subject cannot be deleted while it is used by a document.");
        }

        _unitOfWork.Subjects.Remove(subject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class TierMembershipService : ITierMembershipService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public TierMembershipService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<TierMembershipResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tiers = await _unitOfWork.TierMemberships.GetAllAsync(cancellationToken);
        return tiers.Select(_mapper.Map<TierMembershipResponseDto>).ToList();
    }

    public async Task<TierMembershipResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(id, cancellationToken);
        return tier is null ? null : _mapper.Map<TierMembershipResponseDto>(tier);
    }

    public async Task<TierMembershipResponseDto> CreateAsync(CreateTierMembershipRequestDto request, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.TierMemberships
            .Query()
            .FirstOrDefaultAsync(t => t.TierName == request.TierName, cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException($"Tier with name '{request.TierName}' already exists.");
        }

        var tier = _mapper.Map<Data.Entities.TierMembership>(request);
        await _unitOfWork.TierMemberships.AddAsync(tier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TierMembershipResponseDto>(tier);
    }

    public async Task<TierMembershipResponseDto> UpdateAsync(Guid id, UpdateTierMembershipRequestDto request, CancellationToken cancellationToken = default)
    {
        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(id, cancellationToken);
        if (tier is null)
        {
            throw new KeyNotFoundException($"Tier membership with ID {id} not found.");
        }

        var nameConflict = await _unitOfWork.TierMemberships
            .Query()
            .FirstOrDefaultAsync(t => t.TierName == request.TierName && t.Id != id, cancellationToken);

        if (nameConflict is not null)
        {
            throw new InvalidOperationException($"Tier with name '{request.TierName}' already exists.");
        }

        _mapper.Map(request, tier);
        _unitOfWork.TierMemberships.Update(tier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TierMembershipResponseDto>(tier);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(id, cancellationToken);
        if (tier is null)
        {
            throw new KeyNotFoundException($"Tier membership with ID {id} not found.");
        }

        _unitOfWork.TierMemberships.Remove(tier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}



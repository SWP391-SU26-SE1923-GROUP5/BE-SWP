using AIStudyHub.Business.ultis;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using MediatR;

namespace AIStudyHub.Business.Features.Documents;

public sealed record GetDocumentChunksQuery(Guid Id) : IRequest<IReadOnlyList<DocumentChunk>?>;
public sealed record CreateDocumentChunksCommand(Guid Id) : IRequest<int>;
public sealed record SearchDocumentChunksQuery(Guid Id, string Query, int Top = 5) : IRequest<IReadOnlyList<ChunkingFile.RetrievedChunk>>;

internal sealed class GetDocumentChunksQueryHandler : IRequestHandler<GetDocumentChunksQuery, IReadOnlyList<DocumentChunk>?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDocumentChunksQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<DocumentChunk>?> Handle(GetDocumentChunksQuery request, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(request.Id, cancellationToken);
        if (document == null) return null;

        var chunks = await _unitOfWork.DocumentChunks.FindAsync(dc => dc.DocumentId == request.Id, cancellationToken);
        return chunks.ToList();
    }
}

internal sealed class CreateDocumentChunksCommandHandler : IRequestHandler<CreateDocumentChunksCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmbeddingService _embeddingService;

    public CreateDocumentChunksCommandHandler(IUnitOfWork unitOfWork, IHttpClientFactory httpClientFactory, IEmbeddingService embeddingService)
    {
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _embeddingService = embeddingService;
    }

    public async Task<int> Handle(CreateDocumentChunksCommand request, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");

        var chunkingFile = new ChunkingFile(_httpClientFactory, _embeddingService);
        var chunks = await chunkingFile.CreateChunksAsync(document);

        foreach (var chunk in chunks)
        {
            await _unitOfWork.DocumentChunks.AddAsync(chunk, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return chunks.Count;
    }
}

internal sealed class SearchDocumentChunksQueryHandler : IRequestHandler<SearchDocumentChunksQuery, IReadOnlyList<ChunkingFile.RetrievedChunk>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmbeddingService _embeddingService;

    public SearchDocumentChunksQueryHandler(IUnitOfWork unitOfWork, IHttpClientFactory httpClientFactory, IEmbeddingService embeddingService)
    {
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _embeddingService = embeddingService;
    }

    public async Task<IReadOnlyList<ChunkingFile.RetrievedChunk>> Handle(SearchDocumentChunksQuery request, CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");

        var chunks = await _unitOfWork.DocumentChunks.FindAsync(dc => dc.DocumentId == request.Id, cancellationToken);

        var chunkingFile = new ChunkingFile(_httpClientFactory, _embeddingService);
        var results = await chunkingFile.RetrieveRelevantChunksAsync(chunks, request.Query, request.Top);

        return results;
    }
}

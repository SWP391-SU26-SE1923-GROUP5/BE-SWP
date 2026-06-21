namespace AIStudyHub.Data.Entities;

/// <summary>
/// Represents a processed chunk of a document for AI retrieval workflows.
/// </summary>
public sealed class DocumentChunk : BaseEntity
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the serialized chunk payload.
    /// </summary>
    public string? ChunkJson { get; set; }

    /// <summary>
    /// Gets or sets the vector store ID (Qdrant).
    /// </summary>
    public string? VectorId { get; set; }

    /// <summary>
    /// Gets or sets the order index within the document.
    /// </summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// Gets or sets the related document.
    /// </summary>
    public Document Document { get; set; } = null!;
}

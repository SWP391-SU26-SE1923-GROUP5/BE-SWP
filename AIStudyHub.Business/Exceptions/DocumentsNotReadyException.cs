using AIStudyHub.Business.DTOs.Documents;

namespace AIStudyHub.Business.Exceptions;

public sealed class DocumentsNotReadyException : Exception
{
    public DocumentsNotReadyException(
        IReadOnlyList<BlockingDocumentResponseDto> documents)
        : base("Một hoặc nhiều tài liệu chưa sẵn sàng.")
    {
        Documents = documents;
    }

    public IReadOnlyList<BlockingDocumentResponseDto> Documents { get; }
}

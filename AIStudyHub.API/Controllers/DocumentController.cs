using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
public sealed class DocumentController : CrudControllerBase<DocumentResponseDto, CreateDocumentRequestDto, UpdateDocumentRequestDto>
{
    public DocumentController(IDocumentService service)
        : base(service)
    {
    }
}

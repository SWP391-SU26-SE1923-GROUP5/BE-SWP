using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
public sealed class FlashcardController : CrudControllerBase<FlashcardResponseDto, CreateFlashcardRequestDto, UpdateFlashcardRequestDto>
{
    public FlashcardController(IFlashcardService service)
        : base(service)
    {
    }
}

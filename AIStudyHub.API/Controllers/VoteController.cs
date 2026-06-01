using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
public sealed class VoteController : CrudControllerBase<VoteResponseDto, CreateVoteRequestDto, UpdateVoteRequestDto>
{
    public VoteController(IVoteService service)
        : base(service)
    {
    }
}

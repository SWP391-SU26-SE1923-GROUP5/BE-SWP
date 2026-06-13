using AIStudyHub.Business.DTOs.Subjects;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class SubjectController : CrudControllerBase<SubjectResponseDto, CreateSubjectRequestDto, UpdateSubjectRequestDto>
{
    public SubjectController(ISubjectService service)
        : base(service)
    {
    }
}

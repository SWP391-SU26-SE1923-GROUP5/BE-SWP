using AIStudyHub.Business.DTOs.TierMemberships;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TierMembershipController : CrudControllerBase<TierMembershipResponseDto, CreateTierMembershipRequestDto, UpdateTierMembershipRequestDto>
{
    public TierMembershipController(ITierMembershipService service)
        : base(service)
    {
    }
}

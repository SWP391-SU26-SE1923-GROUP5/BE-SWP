using AIStudyHub.Business.DTOs.TierMemberships;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ITierMembershipService : ICrudService<TierMembershipResponseDto, CreateTierMembershipRequestDto, UpdateTierMembershipRequestDto>
{
}

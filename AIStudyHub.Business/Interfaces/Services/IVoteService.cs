using AIStudyHub.Business.DTOs.Votes;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IVoteService : ICrudService<VoteResponseDto, CreateVoteRequestDto, UpdateVoteRequestDto>
{
}

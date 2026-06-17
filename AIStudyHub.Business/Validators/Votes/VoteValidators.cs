using AIStudyHub.Business.DTOs.Votes;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Votes;

public sealed class CreateVoteRequestDtoValidator : AbstractValidator<CreateVoteRequestDto>
{
    public CreateVoteRequestDtoValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class UpdateVoteRequestDtoValidator : AbstractValidator<UpdateVoteRequestDto>
{
    public UpdateVoteRequestDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
    }
}

using AIStudyHub.Business.DTOs.Documents;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Documents;

public sealed class CreateDocumentRequestDtoValidator : AbstractValidator<CreateDocumentRequestDto>
{
    public CreateDocumentRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.FileName).MaximumLength(255);
        RuleFor(x => x.FileExtension).MaximumLength(255);
        RuleFor(x => x.FileType).MaximumLength(128);
        RuleFor(x => x.ShareStatus).NotEmpty().MaximumLength(20);
    }
}

public sealed class UpdateDocumentRequestDtoValidator : AbstractValidator<UpdateDocumentRequestDto>
{
    public UpdateDocumentRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.FileName).MaximumLength(255);
        RuleFor(x => x.FileExtension).MaximumLength(255);
        RuleFor(x => x.FileType).MaximumLength(128);
        RuleFor(x => x.ShareStatus).NotEmpty().MaximumLength(20);
    }
}

public sealed class ShareDocumentRequestDtoValidator : AbstractValidator<ShareDocumentRequestDto>
{
    public ShareDocumentRequestDtoValidator()
    {
        RuleFor(x => x.SharedUserIds)
            .NotNull()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("SharedUserIds cannot contain empty GUIDs.");
    }
}

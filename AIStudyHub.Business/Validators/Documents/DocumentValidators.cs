using AIStudyHub.Business.DTOs.Documents;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Documents;

public sealed class CreateDocumentRequestDtoValidator : AbstractValidator<CreateDocumentRequestDto>
{
    public CreateDocumentRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.FileUrl).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FileSizeBytes).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateDocumentRequestDtoValidator : AbstractValidator<UpdateDocumentRequestDto>
{
    public UpdateDocumentRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Status).IsInEnum();
    }
}

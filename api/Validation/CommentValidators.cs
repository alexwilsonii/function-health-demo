using FluentValidation;
using TaskManager.Api.Contracts;

namespace TaskManager.Api.Validation;

public class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Comment cannot be empty.")
            .Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Comment cannot be only whitespace.")
            .MaximumLength(2000).WithMessage("Comment must be 2000 characters or fewer.");
    }
}

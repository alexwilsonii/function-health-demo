using FluentValidation;
using TaskManager.Api.Contracts;

namespace TaskManager.Api.Validation;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title cannot be only whitespace.")
            .MaximumLength(200).WithMessage("Title must be 200 characters or fewer.");

        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Priority!.Value).IsInEnum().When(x => x.Priority.HasValue);
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title cannot be only whitespace.")
            .MaximumLength(200).WithMessage("Title must be 200 characters or fewer.");

        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.Status).IsInEnum().WithMessage("Invalid status.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Invalid priority.");
    }
}

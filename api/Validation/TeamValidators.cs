using FluentValidation;
using TaskManager.Api.Contracts;

namespace TaskManager.Api.Validation;

public class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Team name is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Team name cannot be only whitespace.")
            .MaximumLength(100).WithMessage("Team name must be 100 characters or fewer.");
    }
}

public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.");
    }
}

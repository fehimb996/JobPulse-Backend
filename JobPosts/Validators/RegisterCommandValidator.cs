using FluentValidation;
using JobPosts.Commands;

namespace JobPosts.Validators;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
             .NotEmpty().WithMessage("Email is required.")
             .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.");

        RuleFor(x => x.Surname)
            .NotEmpty().WithMessage("Surname is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[?!@#$%^&*.,;:]).+$").WithMessage("Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.")
            .NotEmpty().WithMessage("Password confirmation is required.");
    }
}

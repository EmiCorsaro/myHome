using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Income;

namespace MyHome.Modules.Ledger.Application;

internal sealed class RegisterIncomeRequestValidator : AbstractValidator<RegisterIncomeRequest>
{
    private const int MaxDaysAhead = 366;

    public RegisterIncomeRequestValidator()
    {
        RuleFor(r => r.AccountId)
            .NotEmpty()
            .WithMessage("Choose the account the money came into.");

        RuleFor(r => r.CategoryId)
            .NotEmpty()
            .WithMessage("Choose a category.");

        RuleFor(r => r.Amount)
            .GreaterThan(0m)
            .WithMessage("The amount must be greater than zero.");

        RuleFor(r => r.Description)
            .NotEmpty()
            .WithMessage("Describe what the income was.")
            .MaximumLength(200)
            .WithMessage("The description cannot exceed 200 characters.");

        RuleFor(r => r.OccurredOn)
            .NotEqual(default(DateOnly))
            .WithMessage("Enter the date of the income.")
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(MaxDaysAhead))
            .WithMessage("That date is too far in the future. Check the year.");

        RuleFor(r => r.Recurrence)
            .IsInEnum()
            .WithMessage("Choose how often this income repeats.");

        RuleFor(r => r.ClientMutationId)
            .MaximumLength(64)
            .When(r => r.ClientMutationId is not null);
    }
}

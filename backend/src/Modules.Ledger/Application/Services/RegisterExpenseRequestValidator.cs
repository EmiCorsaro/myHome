using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Expenses;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Shape rules for <see cref="RegisterExpenseRequest"/>.
/// </summary>
/// <remarks>
/// Only what can be judged from the request alone. Whether the account exists and money can
/// actually leave it is a domain rule, checked in
/// <see cref="Domain.JournalEntry.RegisterExpense"/> where skipping the validator does not help.
/// <para>
/// The messages are written for whoever is filling in the form: they come back per field and end
/// up printed under the input.
/// </para>
/// </remarks>
internal sealed class RegisterExpenseRequestValidator : AbstractValidator<RegisterExpenseRequest>
{
    /// <summary>How far into the future an expense may be dated.</summary>
    /// <remarks>
    /// Not zero, because a card payment posting tomorrow is worth recording today. Not unbounded
    /// either, or a typo in the year sails through and distorts every projection.
    /// </remarks>
    private const int MaxDaysAhead = 366;

    /// <summary>Creates the validator.</summary>
    public RegisterExpenseRequestValidator()
    {
        RuleFor(r => r.AccountId)
            .NotEmpty()
            .WithMessage("Choose the account the money came out of.");

        RuleFor(r => r.CategoryId)
            .NotEmpty()
            .WithMessage("Choose a category.");

        RuleFor(r => r.Amount)
            .GreaterThan(0m)
            .WithMessage("The amount must be greater than zero.");

        RuleFor(r => r.Description)
            .NotEmpty()
            .WithMessage("Describe what the expense was.")
            .MaximumLength(200)
            .WithMessage("The description cannot exceed 200 characters.");

        RuleFor(r => r.OccurredOn)
            .NotEqual(default(DateOnly))
            .WithMessage("Enter the date of the expense.")
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(MaxDaysAhead))
            .WithMessage("That date is too far in the future. Check the year.");

        RuleFor(r => r.Recurrence)
            .IsInEnum()
            .WithMessage("Choose how often this expense repeats.");

        RuleFor(r => r.ClientMutationId)
            .MaximumLength(64)
            .When(r => r.ClientMutationId is not null);
    }
}

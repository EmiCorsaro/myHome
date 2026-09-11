using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Transfers;

namespace MyHome.Modules.Ledger.Application;

internal sealed class RegisterTransferRequestValidator : AbstractValidator<RegisterTransferRequest>
{
    public RegisterTransferRequestValidator(TimeProvider clock)
    {
        RuleFor(r => r.FromAccountId)
            .NotEmpty()
            .WithMessage("Choose the account the money came out of.");

        RuleFor(r => r.ToAccountId)
            .NotEmpty()
            .WithMessage("Choose the account the money is going to.");

        RuleFor(r => r.Amount)
            .GreaterThan(0m)
            .WithMessage("The amount must be greater than zero.")
            .Must(HasAtMostTwoDecimals)
            .WithMessage("The amount cannot have more than two decimals.");

        // The description is optional (RF-7): a gap here is not an error, only length is checked.
        RuleFor(r => r.Description)
            .MaximumLength(200)
            .WithMessage("The description cannot exceed 200 characters.");

        RuleFor(r => r.OccurredOn)
            .NotEqual(default(DateOnly))
            .WithMessage("Enter the date of the transfer.")
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            .WithMessage("A future movement is planned, not recorded.");

        RuleFor(r => r.ClientMutationId)
            .MaximumLength(64)
            .When(r => r.ClientMutationId is not null);
    }

    private static bool HasAtMostTwoDecimals(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.ToEven) == amount;
}

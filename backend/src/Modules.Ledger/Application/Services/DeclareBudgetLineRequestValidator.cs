using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Budget;

namespace MyHome.Modules.Ledger.Application;

internal sealed class DeclareBudgetLineRequestValidator : AbstractValidator<DeclareBudgetLineRequest>
{
    /// <param name="clock">
    /// Source of the current instant. "A month already past" is a comparison against today, and a
    /// test has to be able to stand on a known day for it; reading the clock statically is exactly
    /// how that stops being testable.
    /// </param>
    public DeclareBudgetLineRequestValidator(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(r => r.CategoryId)
            .NotEmpty()
            .WithMessage("Elige una categoría.");

        RuleFor(r => r.AccountId)
            .NotEmpty()
            .WithMessage("Elige la cuenta contra la que esperas el movimiento.");

        // Only zero and negative are refused. Decimals are fine: a household that expects 312,50 €
        // of groceries is saying something true, and rounding it for them is not this layer's call.
        RuleFor(r => r.Amount)
            .GreaterThan(0m)
            .WithMessage("El importe debe ser mayor que cero.");

        RuleFor(r => r.Month)
            .Cascade(CascadeMode.Stop)
            .NotEqual(default(DateOnly))
            .WithMessage("Indica el mes que quieres presupuestar.")
            .Must(month => FirstOf(month) >= FirstOf(Today(clock)))
            .WithMessage("Un mes ya pasado solo se consulta: no se puede presupuestar.");

        RuleFor(r => r.AmountMode)
            .Must(mode => mode is BudgetAmountModes.Fixed or BudgetAmountModes.Estimated)
            .WithMessage("Elige si el importe es fijo o estimado.");

        RuleFor(r => r.Sign)
            .Must(sign => sign is BudgetLineSigns.Expense or BudgetLineSigns.Income)
            .WithMessage("Elige si la línea declara un gasto o un ingreso.");

        // Three rules on the same field, deliberately apart. Requiring an origin on income
        // (RF-19) and refusing one on expense (RF-20) are different rules with different
        // messages, and a household that reads "elige el origen" on an expense line learns
        // nothing about what it did wrong.
        RuleFor(r => r.Origin)
            .NotEmpty()
            .When(r => r.Sign == BudgetLineSigns.Income)
            .WithMessage("Elige el origen del ingreso.");

        RuleFor(r => r.Origin)
            .Must(origin => BudgetIncomeOrigins.All.Contains(origin))
            .When(r => r.Sign == BudgetLineSigns.Income && !string.IsNullOrEmpty(r.Origin))
            .WithMessage("Ese origen de ingreso no existe.");

        RuleFor(r => r.Origin)
            .Empty()
            .When(r => r.Sign == BudgetLineSigns.Expense)
            .WithMessage("Una línea de gasto no lleva origen: el origen es del ingreso.");
    }

    private static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static DateOnly FirstOf(DateOnly date) => new(date.Year, date.Month, 1);
}

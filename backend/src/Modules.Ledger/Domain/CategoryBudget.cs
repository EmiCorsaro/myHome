using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public enum BudgetScope
{
    Total = 1,
    Unplanned = 2,
}

public sealed class CategoryBudget : AuditedTenantEntity
{
    private CategoryBudget(
        Guid publicId,
        int householdId,
        int categoryId,
        DateOnly periodStart,
        decimal amount,
        CurrencyCode currency,
        BudgetScope scope,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        HouseholdId = householdId;
        CategoryId = categoryId;
        PeriodStart = periodStart;
        Amount = amount;
        Currency = currency;
        Scope = scope;
        CreatedAt = createdAt;
    }

    public int CategoryId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public decimal Amount { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public BudgetScope Scope { get; private set; }

    public DateOnly PeriodEnd => PeriodStart.AddMonths(1).AddDays(-1);

    public static CategoryBudget Create(
        int householdId,
        Category category,
        DateOnly month,
        Money amount,
        BudgetScope scope = BudgetScope.Total,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException(
                "A budget is a positive allowance. Budgeting zero is deleting the budget.",
                nameof(amount));
        }

        if (category.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "A household cannot budget another household's category.");
        }

        if (category.Kind != CategoryKind.Expense)
        {
            throw new InvalidOperationException(
                $"'{category.Name}' classifies income, and only spending is budgeted this way.");
        }

        return new CategoryBudget(
            Guid.CreateVersion7(),
            householdId,
            category.Id,
            new DateOnly(month.Year, month.Month, 1),
            amount.Amount,
            amount.Currency,
            scope,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public Money Allowance() => Money.Of(Amount, Currency);

    public void Revise(Money amount)
    {
        if (amount.Amount <= 0m)
        {
            throw new ArgumentException("A budget is a positive allowance.", nameof(amount));
        }

        if (amount.Currency != Currency)
        {
            throw new InvalidOperationException(
                $"The budget is set in {Currency} and cannot be revised in {amount.Currency}.");
        }

        Amount = amount.Amount;
    }

    public void Rescope(BudgetScope scope) => Scope = scope;
}

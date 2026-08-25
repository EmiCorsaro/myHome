using System.Reflection;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;

namespace MyHome.Ledger.Tests;

internal static class LedgerFixtures
{
    public const int HouseholdId = 1;

    private static int _nextKey;

    public static Account Bank() =>
        WithKey(Account.Create(
            HouseholdId, "Joint account", AccountType.Checking, CurrencyCode.Euro));

    public static Account Expenses() =>
        WithKey(Account.Create(HouseholdId, "Expenses", AccountType.Expense, CurrencyCode.Euro));

    public static Category Phone(string name = "Mobile") =>
        WithKey(Category.Create(HouseholdId, name, CategoryKind.Expense, 3));

    public static Category Salary() =>
        WithKey(Category.Create(HouseholdId, "Salary", CategoryKind.Income, 2));

    public static Money Euros(decimal amount) => Money.Of(amount, CurrencyCode.Euro);

    public static T WithKey<T>(T entity)
    {
        typeof(T).GetProperty(nameof(Account.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetMethod!
            .Invoke(entity, [++_nextKey]);

        return entity;
    }
}

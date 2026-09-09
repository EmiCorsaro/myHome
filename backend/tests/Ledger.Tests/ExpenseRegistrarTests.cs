using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Registering an expense, as story 009 specifies it. Every test names the requirement it pins
/// down; the map from RF to test lives in <c>progress/impl_009.md</c>. RF-1, RF-2 and RF-3 (double
/// entry, balanced postings) are pinned down at the domain level in
/// <see cref="JournalEntryTests"/>; the tests here confirm the service that sits in front of it
/// carries the same guarantee through.
/// </summary>
public sealed class ExpenseRegistrarTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 9);

    private readonly LedgerDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // RF-1, RF-2: the service reduces the paying account's balance by the amount spent.
    [Fact(DisplayName = "Registering an expense reduces the balance of the paying account")]
    public async Task registering_an_expense_reduces_the_balance_of_the_paying_account()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var registered = await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 42.35m, Today));

        Assert.Equal(42.35m, registered.Amount);
        Assert.Equal(457.65m, await BalanceOf(account));
    }

    // RF-4
    [Fact(DisplayName = "An expense with no description is accepted")]
    public async Task an_expense_with_no_description_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var registered = await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 10m, Today));

        Assert.Equal(string.Empty, registered.Description);
    }

    // RF-4, edge case: a description longer than 200 characters is refused
    [Fact(DisplayName = "A description longer than 200 characters is refused")]
    public async Task a_description_longer_than_200_characters_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today, new string('a', 201))));

        Assert.True(error.Errors.ContainsKey("description"));
    }

    // RF-5
    [Fact(DisplayName = "An expense dated in the past is accepted")]
    public async Task an_expense_dated_in_the_past_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var registered = await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 10m, Today.AddDays(-30), "Compra"));

        Assert.Equal(Today.AddDays(-30), registered.OccurredOn);
    }

    // RF-6
    [Theory(DisplayName = "An amount that is not greater than zero is refused")]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task an_amount_that_is_not_greater_than_zero_is_refused(decimal amount)
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, amount, Today, "Compra")));

        Assert.True(error.Errors.ContainsKey("amount"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-7
    [Fact(DisplayName = "An expense dated tomorrow is refused")]
    public async Task an_expense_dated_tomorrow_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today.AddDays(1), "Compra")));

        Assert.True(error.Errors.ContainsKey("occurredOn"));
    }

    // RF-7, edge case: there is no margin at all, unlike a stale earlier implementation allowed
    [Fact(DisplayName = "An expense dated far in the future is refused, with no margin")]
    public async Task an_expense_dated_far_in_the_future_is_refused_with_no_margin()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today.AddDays(200), "Compra")));

        Assert.True(error.Errors.ContainsKey("occurredOn"));
    }

    // RF-7, edge case: today is not the future
    [Fact(DisplayName = "An expense dated today is accepted")]
    public async Task an_expense_dated_today_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var registered = await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 10m, Today, "Compra"));

        Assert.Equal(Today, registered.OccurredOn);
    }

    // RF-8
    [Fact(DisplayName = "An account belonging to another household is refused")]
    public async Task an_account_belonging_to_another_household_is_refused()
    {
        var account = await Existing("Santander conjunta", householdId: HouseholdId + 1);
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today, "Compra")));

        Assert.True(error.Errors.ContainsKey("accountId"));
    }

    // RF-9
    [Fact(DisplayName = "An archived account is refused")]
    public async Task an_archived_account_is_refused()
    {
        var account = await Existing("Santander conjunta");
        account.Archive();
        await _database.Context.SaveChangesAsync();

        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today, "Compra")));

        Assert.True(error.Errors.ContainsKey("accountId"));
    }

    // RF-10
    [Fact(DisplayName = "A category belonging to another household is refused")]
    public async Task a_category_belonging_to_another_household_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory(householdId: HouseholdId + 1);

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today, "Compra")));

        Assert.True(error.Errors.ContainsKey("categoryId"));
    }

    // RF-10
    [Fact(DisplayName = "An archived category is refused")]
    public async Task an_archived_category_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();
        category.Archive();
        await _database.Context.SaveChangesAsync();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today, "Compra")));

        Assert.True(error.Errors.ContainsKey("categoryId"));
    }

    // RF-11
    [Fact(DisplayName = "A category of type income is refused for an expense")]
    public async Task a_category_of_type_income_is_refused_for_an_expense()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today, "Compra")));

        Assert.True(error.Errors.ContainsKey("categoryId"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-12
    [Fact(DisplayName = "An expense dated before the account's opening balance is refused")]
    public async Task an_expense_dated_before_the_accounts_opening_balance_is_refused()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 100m, Today.AddDays(-10));
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10m, Today.AddDays(-11), "Compra")));

        Assert.Equal(
            "An expense cannot be dated before the account's opening balance.",
            Assert.Single(error.Errors["occurredOn"]));
        Assert.Equal(1, await _database.Context.Entries.CountAsync());
    }

    // RF-12, edge case: an account with no opening balance declared has no lower bound at all
    [Fact(DisplayName = "An account with no opening balance declared accepts any past date")]
    public async Task an_account_with_no_opening_balance_declared_accepts_any_past_date()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var registered = await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 10m, new DateOnly(2000, 1, 1), "Compra"));

        Assert.Equal(new DateOnly(2000, 1, 1), registered.OccurredOn);
    }

    // RF-12, edge case: the day of the opening balance itself is not before it
    [Fact(DisplayName = "An expense dated on the day of the opening balance is accepted")]
    public async Task an_expense_dated_on_the_day_of_the_opening_balance_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 100m, Today.AddDays(-10));
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var registered = await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 10m, Today.AddDays(-10), "Compra"));

        Assert.Equal(Today.AddDays(-10), registered.OccurredOn);
    }

    // RF-13
    [Fact(DisplayName = "Resending the same mutation does not duplicate the expense")]
    public async Task resending_the_same_mutation_does_not_duplicate_the_expense()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var request = new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 10m, Today, "Compra", ClientMutationId: "abc-123");

        var first = await expenses.RegisterAsync(request);
        var second = await expenses.RegisterAsync(request);

        Assert.Equal(first.Id, second.Id);
        Assert.False(first.WasAlreadyRegistered);
        Assert.True(second.WasAlreadyRegistered);
        Assert.Equal(1, await _database.Context.Entries.CountAsync(e => e.Kind == EntryKind.Expense));
    }

    // RF-14
    [Fact(DisplayName = "An expense that leaves the account balance negative is registered anyway")]
    public async Task an_expense_that_leaves_the_account_balance_negative_is_registered_anyway()
    {
        var account = await Existing("Tarjeta", AccountType.CreditCard);
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var registered = await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 50m, Today, "Compra"));

        Assert.Equal(50m, registered.Amount);
        Assert.Equal(-50m, await BalanceOf(account));
    }

    // RF-15, RF-16
    [Fact(DisplayName = "An amount of exactly two decimals is accepted")]
    public async Task an_amount_of_exactly_two_decimals_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var registered = await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 10.99m, Today, "Compra"));

        Assert.Equal(10.99m, registered.Amount);
    }

    // RF-16
    [Fact(DisplayName = "An amount with more than two decimals is refused, not rounded")]
    public async Task an_amount_with_more_than_two_decimals_is_refused_not_rounded()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new RegisterExpenseRequest(
                account.PublicId, category.PublicId, 10.999m, Today, "Compra")));

        Assert.True(error.Errors.ContainsKey("amount"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-17
    [Fact(DisplayName = "An expense classifies against exactly one category")]
    public async Task an_expense_classifies_against_exactly_one_category()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 10m, Today, "Compra"));

        var entry = await _database.Context.Entries
            .Include(e => e.Postings)
            .SingleAsync(e => e.Kind == EntryKind.Expense);

        var categorized = entry.Postings.Where(p => p.CategoryId is not null).ToList();

        Assert.Single(categorized);
        Assert.Equal(category.Id, categorized[0].CategoryId);
    }

    // RF-18
    [Fact(DisplayName = "Any member of the household may register an expense")]
    public async Task any_member_of_the_household_may_register_an_expense()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var byOneMember = await ExpenseRegistrarFor(memberId: 7).RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 10m, Today, "Compra"));
        Assert.NotNull(byOneMember);

        var byAnother = await ExpenseRegistrarFor(memberId: 42).RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 10m, Today, "Compra"));
        Assert.NotNull(byAnother);

        var byAnonymous = await ExpenseRegistrarFor(memberId: null).RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 10m, Today, "Compra"));
        Assert.NotNull(byAnonymous);
    }

    private ExpenseRegistrar ExpenseRegistrarFor(int? memberId = null) =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId, memberId),
            new RegisterExpenseRequestValidator(),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private async Task<Account> Existing(
        string name,
        AccountType type = AccountType.Checking,
        int householdId = HouseholdId)
    {
        var account = Account.Create(householdId, name, type, CurrencyCode.Euro);

        _database.Context.Accounts.Add(account);
        await _database.Context.SaveChangesAsync();

        return account;
    }

    private async Task<Category> NewExpenseCategory(int householdId = HouseholdId)
    {
        var category = Category.Create(
            householdId, $"Gasto {Guid.CreateVersion7()}", CategoryKind.Expense, colorIndex: 1);

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }

    private async Task<Category> NewIncomeCategory()
    {
        var category = Category.Create(
            HouseholdId, $"Ingreso {Guid.CreateVersion7()}", CategoryKind.Income, colorIndex: 2);

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }

    private async Task Opening(Account account, decimal amount, DateOnly? occurredOn = null)
    {
        _database.Context.Entries.Add(JournalEntry.OpenBalance(
            account.HouseholdId, account, Euros(amount), occurredOn ?? Today.AddDays(-30)));

        await _database.Context.SaveChangesAsync();
    }

    private async Task<decimal> BalanceOf(Account account) =>
        await _database.Context.Postings
            .Where(p => p.AccountId == account.Id)
            .SumAsync(p => p.AmountBase);
}

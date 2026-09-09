using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Incomes;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Registering an income, as story 010 specifies it. Every test names the requirement it pins
/// down; the map from RF to test lives in <c>progress/impl_010.md</c>. RF-1, RF-2 and RF-3
/// (double entry, balanced postings) are pinned down at the domain level in
/// <see cref="JournalEntryTests"/>; the tests here confirm the service that sits in front of it
/// carries the same guarantee through.
/// </summary>
public sealed class IncomeRegistrarTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 9);

    private readonly LedgerDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // RF-1, RF-2: the service increases the deposit account's balance by the amount received.
    [Fact(DisplayName = "Registering an income increases the balance of the deposit account")]
    public async Task registering_an_income_increases_the_balance_of_the_deposit_account()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var registered = await incomes.RegisterAsync(
            new RegisterIncomeRequest(account.PublicId, category.PublicId, 1500m, Today));

        Assert.Equal(1500m, registered.Amount);
        Assert.Equal(2000m, await BalanceOf(account));
    }

    // RF-4
    [Fact(DisplayName = "An income with no description is accepted")]
    public async Task an_income_with_no_description_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var registered = await incomes.RegisterAsync(
            new RegisterIncomeRequest(account.PublicId, category.PublicId, 10m, Today));

        Assert.Equal(string.Empty, registered.Description);
    }

    // RF-4, edge case: a description longer than 200 characters is refused
    [Fact(DisplayName = "A description longer than 200 characters is refused")]
    public async Task a_description_longer_than_200_characters_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today, new string('a', 201))));

        Assert.True(error.Errors.ContainsKey("description"));
    }

    // RF-5
    [Fact(DisplayName = "An income dated in the past is accepted")]
    public async Task an_income_dated_in_the_past_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var registered = await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10m, Today.AddDays(-30), "Nómina"));

        Assert.Equal(Today.AddDays(-30), registered.OccurredOn);
    }

    // RF-6
    [Theory(DisplayName = "An amount that is not greater than zero is refused")]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task an_amount_that_is_not_greater_than_zero_is_refused(decimal amount)
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, amount, Today, "Nómina")));

        Assert.True(error.Errors.ContainsKey("amount"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-7
    [Fact(DisplayName = "An income dated tomorrow is refused")]
    public async Task an_income_dated_tomorrow_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today.AddDays(1), "Nómina")));

        Assert.True(error.Errors.ContainsKey("occurredOn"));
    }

    // RF-7, edge case: there is no margin at all
    [Fact(DisplayName = "An income dated far in the future is refused, with no margin")]
    public async Task an_income_dated_far_in_the_future_is_refused_with_no_margin()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today.AddDays(200), "Nómina")));

        Assert.True(error.Errors.ContainsKey("occurredOn"));
    }

    // RF-7, edge case: today is not the future
    [Fact(DisplayName = "An income dated today is accepted")]
    public async Task an_income_dated_today_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var registered = await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10m, Today, "Nómina"));

        Assert.Equal(Today, registered.OccurredOn);
    }

    // RF-8
    [Fact(DisplayName = "An account belonging to another household is refused")]
    public async Task an_account_belonging_to_another_household_is_refused()
    {
        var account = await Existing("Santander conjunta", householdId: HouseholdId + 1);
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today, "Nómina")));

        Assert.True(error.Errors.ContainsKey("accountId"));
    }

    // RF-9
    [Fact(DisplayName = "An archived account is refused")]
    public async Task an_archived_account_is_refused()
    {
        var account = await Existing("Santander conjunta");
        account.Archive();
        await _database.Context.SaveChangesAsync();

        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today, "Nómina")));

        Assert.True(error.Errors.ContainsKey("accountId"));
    }

    // RF-10
    [Fact(DisplayName = "A category belonging to another household is refused")]
    public async Task a_category_belonging_to_another_household_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory(householdId: HouseholdId + 1);

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today, "Nómina")));

        Assert.True(error.Errors.ContainsKey("categoryId"));
    }

    // RF-10
    [Fact(DisplayName = "An archived category is refused")]
    public async Task an_archived_category_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();
        category.Archive();
        await _database.Context.SaveChangesAsync();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today, "Nómina")));

        Assert.True(error.Errors.ContainsKey("categoryId"));
    }

    // RF-11
    [Fact(DisplayName = "A category of type expense is refused for an income")]
    public async Task a_category_of_type_expense_is_refused_for_an_income()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today, "Nómina")));

        Assert.True(error.Errors.ContainsKey("categoryId"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-12
    [Fact(DisplayName = "Resending the same mutation does not duplicate the income")]
    public async Task resending_the_same_mutation_does_not_duplicate_the_income()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var request = new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10m, Today, "Nómina", ClientMutationId: "abc-123");

        var first = await incomes.RegisterAsync(request);
        var second = await incomes.RegisterAsync(request);

        Assert.Equal(first.Id, second.Id);
        Assert.False(first.WasAlreadyRegistered);
        Assert.True(second.WasAlreadyRegistered);
        Assert.Equal(1, await _database.Context.Entries.CountAsync(e => e.Kind == EntryKind.Income));
    }

    // RF-13
    [Fact(DisplayName = "An income into a credit card is refused, with a message about the expense")]
    public async Task an_income_into_a_credit_card_is_refused_with_a_message_about_the_expense()
    {
        var account = await Existing("Tarjeta", AccountType.CreditCard);
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 50m, Today, "Devolución")));

        var message = Assert.Single(error.Errors["accountId"]);
        Assert.Contains("expense", message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_database.Context.Entries);
    }

    // RF-14, RF-16: a member that resolves is stored on the entry
    [Fact(DisplayName = "A member belonging to the household is stored as the perceiver")]
    public async Task a_member_belonging_to_the_household_is_stored_as_the_perceiver()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();
        var memberPublicId = Guid.CreateVersion7();

        var incomes = IncomeRegistrarFor(
            resolvableMembers: new Dictionary<Guid, int> { [memberPublicId] = 7 });

        await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10m, Today, "Nómina", MemberId: memberPublicId));

        var entry = await _database.Context.Entries
            .Include(e => e.Postings)
            .SingleAsync(e => e.Kind == EntryKind.Income);

        Assert.All(entry.Postings, p => Assert.Equal(7, p.MemberId));
    }

    // RF-15: no member indicated leaves the income as the household's, unattributed
    [Fact(DisplayName = "An income with no member indicated is registered without a perceiver")]
    public async Task an_income_with_no_member_indicated_is_registered_without_a_perceiver()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10m, Today, "Nómina"));

        var entry = await _database.Context.Entries
            .Include(e => e.Postings)
            .SingleAsync(e => e.Kind == EntryKind.Income);

        Assert.All(entry.Postings, p => Assert.Null(p.MemberId));
    }

    // RF-16: a member that does not resolve is refused
    [Fact(DisplayName = "A member not belonging to the household is refused")]
    public async Task a_member_not_belonging_to_the_household_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today, "Nómina",
                MemberId: Guid.CreateVersion7())));

        Assert.True(error.Errors.ContainsKey("memberId"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-17
    [Fact(DisplayName = "An income dated before the account's opening balance is refused")]
    public async Task an_income_dated_before_the_accounts_opening_balance_is_refused()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 100m, Today.AddDays(-10));
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10m, Today.AddDays(-11), "Nómina")));

        Assert.Equal(
            "An income cannot be dated before the account's opening balance.",
            Assert.Single(error.Errors["occurredOn"]));
        Assert.Equal(1, await _database.Context.Entries.CountAsync());
    }

    // RF-17, edge case: an account with no opening balance declared has no lower bound at all
    [Fact(DisplayName = "An account with no opening balance declared accepts any past date")]
    public async Task an_account_with_no_opening_balance_declared_accepts_any_past_date()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var registered = await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10m, new DateOnly(2000, 1, 1), "Nómina"));

        Assert.Equal(new DateOnly(2000, 1, 1), registered.OccurredOn);
    }

    // RF-17, edge case: the day of the opening balance itself is not before it
    [Fact(DisplayName = "An income dated on the day of the opening balance is accepted")]
    public async Task an_income_dated_on_the_day_of_the_opening_balance_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 100m, Today.AddDays(-10));
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var registered = await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10m, Today.AddDays(-10), "Nómina"));

        Assert.Equal(Today.AddDays(-10), registered.OccurredOn);
    }

    // RF-18, RF-19
    [Fact(DisplayName = "An amount of exactly two decimals is accepted")]
    public async Task an_amount_of_exactly_two_decimals_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var registered = await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10.99m, Today, "Nómina"));

        Assert.Equal(10.99m, registered.Amount);
    }

    // RF-19
    [Fact(DisplayName = "An amount with more than two decimals is refused, not rounded")]
    public async Task an_amount_with_more_than_two_decimals_is_refused_not_rounded()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => incomes.RegisterAsync(new RegisterIncomeRequest(
                account.PublicId, category.PublicId, 10.999m, Today, "Nómina")));

        Assert.True(error.Errors.ContainsKey("amount"));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-3: an income classifies against exactly one category
    [Fact(DisplayName = "An income classifies against exactly one category")]
    public async Task an_income_classifies_against_exactly_one_category()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var incomes = IncomeRegistrarFor();

        await incomes.RegisterAsync(new RegisterIncomeRequest(
            account.PublicId, category.PublicId, 10m, Today, "Nómina"));

        var entry = await _database.Context.Entries
            .Include(e => e.Postings)
            .SingleAsync(e => e.Kind == EntryKind.Income);

        var categorized = entry.Postings.Where(p => p.CategoryId is not null).ToList();

        Assert.Single(categorized);
        Assert.Equal(category.Id, categorized[0].CategoryId);
    }

    // RF-20
    [Fact(DisplayName = "Any member of the household may register an income")]
    public async Task any_member_of_the_household_may_register_an_income()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewIncomeCategory();

        var byOneMember = await IncomeRegistrarFor(memberId: 7).RegisterAsync(
            new RegisterIncomeRequest(account.PublicId, category.PublicId, 10m, Today, "Nómina"));
        Assert.NotNull(byOneMember);

        var byAnother = await IncomeRegistrarFor(memberId: 42).RegisterAsync(
            new RegisterIncomeRequest(account.PublicId, category.PublicId, 10m, Today, "Nómina"));
        Assert.NotNull(byAnother);

        var byAnonymous = await IncomeRegistrarFor(memberId: null).RegisterAsync(
            new RegisterIncomeRequest(account.PublicId, category.PublicId, 10m, Today, "Nómina"));
        Assert.NotNull(byAnonymous);
    }

    private IncomeRegistrar IncomeRegistrarFor(
        int? memberId = null,
        IReadOnlyDictionary<Guid, int>? resolvableMembers = null) =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId, memberId),
            new RegisterIncomeRequestValidator(),
            new TestHouseholdDirectory(CurrencyCode.Euro, resolvableMembers));

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

    private async Task<Category> NewIncomeCategory(int householdId = HouseholdId)
    {
        var category = Category.Create(
            householdId, $"Ingreso {Guid.CreateVersion7()}", CategoryKind.Income, colorIndex: 2);

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }

    private async Task<Category> NewExpenseCategory()
    {
        var category = Category.Create(
            HouseholdId, $"Gasto {Guid.CreateVersion7()}", CategoryKind.Expense, colorIndex: 1);

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

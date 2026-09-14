using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// The opening balance of an account, as story 003 specifies it. Every test names the requirement
/// it pins down; the map from RF to test lives in <c>progress/impl_003.md</c>.
/// </summary>
public sealed class OpeningBalanceRegistrarTests : IDisposable
{
    /// <summary>A fixed instant, so "today" and "tomorrow" mean the same thing on every run.</summary>
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Today in the household's time zone (Europe/Madrid) at <see cref="Now"/>.</summary>
    private static readonly DateOnly Today = new(2026, 9, 9);

    private readonly LedgerDatabase _database = new();
    private readonly OpeningBalanceRegistrar _registrar;

    public OpeningBalanceRegistrarTests()
    {
        _registrar = NewRegistrar();
    }

    public void Dispose() => _database.Dispose();

    // RF-1
    [Fact(DisplayName = "A declared opening balance leaves the account at that amount and date")]
    public async Task a_declared_opening_balance_leaves_the_account_at_that_amount_and_date()
    {
        var account = await Existing("Santander conjunta");

        var declared = await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(1234.56m, Today.AddDays(-10)));

        Assert.Equal(account.PublicId, declared.AccountId);
        Assert.Equal("Santander conjunta", declared.AccountName);
        Assert.Equal("EUR", declared.Currency);
        Assert.Equal(1234.56m, declared.Amount);
        Assert.Equal(Today.AddDays(-10), declared.OccurredOn);
        Assert.Equal(1234.56m, declared.AccountBalance);
        Assert.Equal(1234.56m, await BalanceOf(account));

        var entry = await _database.Context.Entries
            .Include(e => e.Postings)
            .SingleAsync(e => e.OpeningAccountId == account.Id);

        Assert.Equal(EntryKind.Opening, entry.Kind);
        Assert.Equal(Today.AddDays(-10), entry.OccurredOn);
        Assert.Equal(account.Id, Assert.Single(entry.Postings).AccountId);
    }

    // RF-2: the opening balance is neither income nor expense of any period
    [Fact(DisplayName = "An opening balance is not income nor expense of the month it falls in")]
    public async Task an_opening_balance_is_not_income_nor_expense_of_the_month_it_falls_in()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(2000m, Today));

        var summary = await NewDashboard().GetMonthlySummaryAsync(Today);

        Assert.Equal(0m, summary.Income);
        Assert.Equal(0m, summary.Expense);
        Assert.Equal(0m, summary.Net);

        // It is still money the household has: it shows up in the balance, not in the flows.
        Assert.Equal(2000m, summary.TrackedBalance);
    }

    // RF-3: and it is nowhere in the spend-by-category report
    [Fact(DisplayName = "An opening balance stays out of the spend-by-category report")]
    public async Task an_opening_balance_stays_out_of_the_spend_by_category_report()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(2000m, Today));

        var summary = await NewDashboard().GetMonthlySummaryAsync(Today);

        Assert.Empty(summary.ByCategory);

        var posting = await _database.Context.Postings.SingleAsync();

        Assert.Null(posting.CategoryId);
    }

    // RF-4
    [Fact(DisplayName = "A credit card starts with the debt it owes")]
    public async Task a_credit_card_starts_with_the_debt_it_owes()
    {
        var account = await Existing("Tarjeta Santander", AccountType.CreditCard);

        var declared = await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(-450.75m, Today));

        Assert.Equal(-450.75m, declared.Amount);
        Assert.Equal(-450.75m, declared.AccountBalance);
        Assert.Equal(-450.75m, await BalanceOf(account));
    }

    // RF-5
    [Theory(DisplayName = "An opening balance of zero is a statement like any other")]
    [InlineData(AccountType.Checking)]
    [InlineData(AccountType.CreditCard)]
    public async Task an_opening_balance_of_zero_is_a_statement_like_any_other(AccountType type)
    {
        var account = await Existing("Cuenta", type);

        var declared = await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(0m, Today));

        Assert.Equal(0m, declared.Amount);
        Assert.Equal(1, await _database.Context.Entries.CountAsync());
    }

    // RF-6
    [Fact(DisplayName = "An opening balance dated in the future is refused")]
    public async Task an_opening_balance_dated_in_the_future_is_refused()
    {
        var account = await Existing("Santander conjunta");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(1))));

        Assert.Equal(
            "La fecha del saldo inicial no puede ser futura.",
            Assert.Single(error.Errors["occurredOn"]));
        Assert.Empty(_database.Context.Entries);
    }

    // RF-6, edge case: today is not the future
    [Fact(DisplayName = "An opening balance dated today is accepted")]
    public async Task an_opening_balance_dated_today_is_accepted()
    {
        var account = await Existing("Santander conjunta");

        var declared = await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today));

        Assert.Equal(Today, declared.OccurredOn);
    }

    // RF-7
    [Fact(DisplayName = "Declaring a second opening balance points at the edit instead")]
    public async Task declaring_a_second_opening_balance_points_at_the_edit_instead()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                account.PublicId, new DeclareOpeningBalanceRequest(200m, Today)));

        Assert.Equal(
            "Esta cuenta ya tiene un saldo inicial declarado. Edítalo en lugar de declararlo otra vez.",
            Assert.Single(error.Errors["openingBalance"]));

        // Nothing was written: the balance is still the one declared first.
        Assert.Equal(1, await _database.Context.Entries.CountAsync());
        Assert.Equal(100m, await BalanceOf(account));
    }

    // RF-7: the rule is the database's, not only the service's
    [Fact(DisplayName = "The database refuses a second opening balance for the same account")]
    public async Task the_database_refuses_a_second_opening_balance_for_the_same_account()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today));

        // Straight past the service, as a member who won the race a millisecond earlier would.
        _database.Context.Entries.Add(JournalEntry.OpenBalance(
            HouseholdId, account, Euros(200m), Today));

        await Assert.ThrowsAsync<DbUpdateException>(() => _database.Context.SaveChangesAsync());
    }

    // RF-8
    [Fact(DisplayName = "A movement earlier than the opening balance is out of bounds")]
    public async Task a_movement_earlier_than_the_opening_balance_is_out_of_bounds()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-5)));

        Assert.True(await OpeningBalances.IsBeforeOpeningBalanceAsync(
            _database.Context, account.Id, Today.AddDays(-6)));

        // The day of the opening balance itself is inside the bounds, and so is anything after it.
        Assert.False(await OpeningBalances.IsBeforeOpeningBalanceAsync(
            _database.Context, account.Id, Today.AddDays(-5)));
        Assert.False(await OpeningBalances.IsBeforeOpeningBalanceAsync(
            _database.Context, account.Id, Today));
    }

    // RF-8: only an account with an opening balance declared has a lower bound at all
    [Fact(DisplayName = "An account with no opening balance declared bounds nothing")]
    public async Task an_account_with_no_opening_balance_declared_bounds_nothing()
    {
        var account = await Existing("Santander conjunta");

        Assert.Null(await OpeningBalances.DeclaredDateAsync(_database.Context, account.Id));
        Assert.False(await OpeningBalances.IsBeforeOpeningBalanceAsync(
            _database.Context, account.Id, new DateOnly(1999, 1, 1)));
    }

    // RF-9
    [Fact(DisplayName = "Editing the opening balance recomputes the balance of the account")]
    public async Task editing_the_opening_balance_recomputes_the_balance_of_the_account()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-5)));

        var amended = await _registrar.AmendAsync(
            account.PublicId, new AmendOpeningBalanceRequest(250m, Today.AddDays(-3)));

        Assert.Equal(250m, amended.Amount);
        Assert.Equal(Today.AddDays(-3), amended.OccurredOn);
        Assert.Equal(250m, amended.AccountBalance);
        Assert.Equal(250m, await BalanceOf(account));

        // Corrected in place: the account has one opening balance, not two.
        Assert.Equal(1, await _database.Context.Entries.CountAsync());
    }

    // RF-9, edge case: only the amount changes
    [Fact(DisplayName = "An edit that only changes the amount keeps the date")]
    public async Task an_edit_that_only_changes_the_amount_keeps_the_date()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-5)));

        var amended = await _registrar.AmendAsync(
            account.PublicId, new AmendOpeningBalanceRequest(80m, OccurredOn: null));

        Assert.Equal(80m, amended.Amount);
        Assert.Equal(Today.AddDays(-5), amended.OccurredOn);
    }

    // RF-9: and the mirror case, only the date changes
    [Fact(DisplayName = "An edit that only changes the date keeps the amount")]
    public async Task an_edit_that_only_changes_the_date_keeps_the_amount()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-5)));

        var amended = await _registrar.AmendAsync(
            account.PublicId, new AmendOpeningBalanceRequest(Amount: null, Today.AddDays(-4)));

        Assert.Equal(100m, amended.Amount);
        Assert.Equal(Today.AddDays(-4), amended.OccurredOn);
    }

    // RF-9: an account with no opening balance has nothing to edit
    [Fact(DisplayName = "Editing an opening balance that was never declared is refused")]
    public async Task editing_an_opening_balance_that_was_never_declared_is_refused()
    {
        var account = await Existing("Santander conjunta");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.AmendAsync(
                account.PublicId, new AmendOpeningBalanceRequest(100m, Today)));

        Assert.Equal(
            "Esta cuenta todavía no tiene un saldo inicial declarado. Decláralo primero.",
            Assert.Single(error.Errors["openingBalance"]));
    }

    // RF-10
    [Fact(DisplayName = "The opening balance of an account with movements can still be edited")]
    public async Task the_opening_balance_of_an_account_with_movements_can_still_be_edited()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-10)));

        await Movement(account, 40m, Today.AddDays(-2));

        var amended = await _registrar.AmendAsync(
            account.PublicId, new AmendOpeningBalanceRequest(300m, OccurredOn: null));

        Assert.Equal(300m, amended.Amount);

        // 300 declared, 40 spent: the movement is untouched and counted on top of the new figure.
        Assert.Equal(260m, amended.AccountBalance);
        Assert.Equal(260m, await BalanceOf(account));
    }

    // RF-10: an account that got its movements in before anyone declared where it started
    [Fact(DisplayName = "An account that already has movements can declare its opening balance")]
    public async Task an_account_that_already_has_movements_can_declare_its_opening_balance()
    {
        var account = await Existing("Santander conjunta");

        await Movement(account, 40m, Today.AddDays(-2));

        var declared = await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-10)));

        Assert.Equal(100m, declared.Amount);
        Assert.Equal(60m, declared.AccountBalance);
    }

    // RF-11
    [Fact(DisplayName = "An edit that dates the opening balance in the future is refused")]
    public async Task an_edit_that_dates_the_opening_balance_in_the_future_is_refused()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-5)));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.AmendAsync(
                account.PublicId, new AmendOpeningBalanceRequest(200m, Today.AddDays(1))));

        Assert.Equal(
            "La fecha del saldo inicial no puede ser futura.",
            Assert.Single(error.Errors["occurredOn"]));

        // Neither half of the edit was applied.
        var opening = await _database.Context.Entries
            .Include(e => e.Postings)
            .SingleAsync(e => e.OpeningAccountId == account.Id);

        Assert.Equal(Today.AddDays(-5), opening.OccurredOn);
        Assert.Equal(100m, Assert.Single(opening.Postings).Amount);
    }

    // RF-12
    [Fact(DisplayName = "An edit that would strand a movement behind the opening balance is refused")]
    public async Task an_edit_that_would_strand_a_movement_behind_the_opening_balance_is_refused()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-10)));

        await Movement(account, 40m, Today.AddDays(-6));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.AmendAsync(
                account.PublicId, new AmendOpeningBalanceRequest(200m, Today.AddDays(-5))));

        Assert.Equal(
            "La cuenta tiene movimientos anteriores a esa fecha.",
            Assert.Single(error.Errors["occurredOn"]));
        Assert.Equal(60m, await BalanceOf(account));
    }

    // RF-12, edge case: a movement on the very day of the new opening balance is not behind it
    [Fact(DisplayName = "An edit dated on the day of the earliest movement is accepted")]
    public async Task an_edit_dated_on_the_day_of_the_earliest_movement_is_accepted()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-10)));

        await Movement(account, 40m, Today.AddDays(-6));

        var amended = await _registrar.AmendAsync(
            account.PublicId, new AmendOpeningBalanceRequest(Amount: null, Today.AddDays(-6)));

        Assert.Equal(Today.AddDays(-6), amended.OccurredOn);
    }

    // RF-12: a movement on another account of the household is none of this account's business
    [Fact(DisplayName = "A movement on another account does not hold the opening balance back")]
    public async Task a_movement_on_another_account_does_not_hold_the_opening_balance_back()
    {
        var account = await Existing("Santander conjunta");
        var other = await Existing("Efectivo", AccountType.Cash);

        await _registrar.DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-10)));

        await Movement(other, 40m, Today.AddDays(-8));

        var amended = await _registrar.AmendAsync(
            account.PublicId, new AmendOpeningBalanceRequest(Amount: null, Today.AddDays(-5)));

        Assert.Equal(Today.AddDays(-5), amended.OccurredOn);
    }

    // RF-13
    [Fact(DisplayName = "Any member of the household may declare and edit an opening balance")]
    public async Task any_member_of_the_household_may_declare_and_edit_an_opening_balance()
    {
        var account = await Existing("Santander conjunta");

        await NewRegistrar(memberId: 7).DeclareAsync(
            account.PublicId, new DeclareOpeningBalanceRequest(100m, Today.AddDays(-5)));

        var byAnother = await NewRegistrar(memberId: 42).AmendAsync(
            account.PublicId, new AmendOpeningBalanceRequest(150m, OccurredOn: null));

        Assert.Equal(150m, byAnother.Amount);

        var byAnonymous = await NewRegistrar(memberId: null).AmendAsync(
            account.PublicId, new AmendOpeningBalanceRequest(175m, OccurredOn: null));

        Assert.Equal(175m, byAnonymous.Amount);
    }

    // An account of another household is not this household's to declare anything about.
    [Fact(DisplayName = "An account of another household is not available")]
    public async Task an_account_of_another_household_is_not_available()
    {
        var account = await Existing("Santander conjunta", householdId: HouseholdId + 1);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                account.PublicId, new DeclareOpeningBalanceRequest(100m, Today)));

        Assert.Equal("Esa cuenta no está disponible.", Assert.Single(error.Errors["accountId"]));
        Assert.Empty(_database.Context.Entries);
    }

    // An archived account, and an account nobody ever opened, get the very same answer.
    [Fact(DisplayName = "An archived or unknown account is not available")]
    public async Task an_archived_or_unknown_account_is_not_available()
    {
        var archived = await Existing("Vieja", archived: true);

        var forArchived = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.DeclareAsync(
                archived.PublicId, new DeclareOpeningBalanceRequest(100m, Today)));

        var forUnknown = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.AmendAsync(
                Guid.CreateVersion7(), new AmendOpeningBalanceRequest(100m, Today)));

        Assert.True(forArchived.Errors.ContainsKey("accountId"));
        Assert.True(forUnknown.Errors.ContainsKey("accountId"));
    }

    // An edit has to say what it changes.
    [Fact(DisplayName = "An edit that changes neither the amount nor the date is refused")]
    public async Task an_edit_that_changes_neither_the_amount_nor_the_date_is_refused()
    {
        var account = await Existing("Santander conjunta");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.AmendAsync(
                account.PublicId, new AmendOpeningBalanceRequest(Amount: null, OccurredOn: null)));

        Assert.Equal(
            "Indica el nuevo importe, la nueva fecha o ambos.",
            Assert.Single(error.Errors["amount"]));
    }

    private OpeningBalanceRegistrar NewRegistrar(int? memberId = null) =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId, memberId),
            new TestHouseholdDirectory(CurrencyCode.Euro),
            new FixedTimeProvider(Now));

    private DashboardQuery NewDashboard() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new TestHouseholdDirectory(CurrencyCode.Euro),
            new DashboardMonthRequestValidator(),
            TimeProvider.System);

    private async Task<Account> Existing(
        string name,
        AccountType type = AccountType.Checking,
        int householdId = HouseholdId,
        bool archived = false)
    {
        var account = Account.Create(householdId, name, type, CurrencyCode.Euro);

        if (archived)
        {
            account.Archive();
        }

        _database.Context.Accounts.Add(account);
        await _database.Context.SaveChangesAsync();

        return account;
    }

    /// <summary>
    /// An expense recorded against the account, so tests about editing have something to trip over.
    /// </summary>
    /// <param name="account">Account the money leaves.</param>
    /// <param name="amount">How much is spent.</param>
    /// <param name="occurredOn">When it was spent.</param>
    private async Task Movement(Account account, decimal amount, DateOnly occurredOn)
    {
        var expenses = await _database.Context.Accounts
            .FirstOrDefaultAsync(a => a.Type == AccountType.Expense
                && a.HouseholdId == account.HouseholdId);

        if (expenses is null)
        {
            expenses = Account.Create(
                account.HouseholdId, "Expenses", AccountType.Expense, CurrencyCode.Euro);

            _database.Context.Accounts.Add(expenses);
            await _database.Context.SaveChangesAsync();
        }

        var category = Category.Create(
            account.HouseholdId,
            $"Gasto {Guid.CreateVersion7()}",
            CategoryKind.Expense,
            colorIndex: 1);

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        _database.Context.Entries.Add(JournalEntry.RegisterExpense(
            account.HouseholdId,
            occurredOn,
            "Compra",
            paidFrom: account,
            expenseAccount: expenses,
            category: category,
            amount: Euros(amount)));

        await _database.Context.SaveChangesAsync();
    }

    private async Task<decimal> BalanceOf(Account account) =>
        await _database.Context.Postings
            .Where(p => p.AccountId == account.Id)
            .SumAsync(p => p.AmountBase);
}

/// <summary>
/// A clock stopped at a known instant, so a test can say "tomorrow" and mean it.
/// </summary>
/// <param name="now">The instant every call reports.</param>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

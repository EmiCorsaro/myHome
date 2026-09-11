using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Marking an account as controlled and fixing its minimum buffer, as story 004 specifies it.
/// Every test names the requirement it pins down; the map from RF to test lives in
/// <c>progress/impl_004.md</c>.
/// </summary>
public sealed class AccountControlRegistrarTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 9);

    private readonly LedgerDatabase _database = new();
    private readonly AccountControlRegistrar _registrar;

    public AccountControlRegistrarTests()
    {
        _registrar = NewRegistrar();
    }

    public void Dispose() => _database.Dispose();

    // RF-1
    [Fact(DisplayName = "Marking an account as not controlled excludes it from the disponible real")]
    public async Task marking_an_account_as_not_controlled_excludes_it_from_the_disponible_real()
    {
        var tracked = await Existing("Santander conjunta");
        var toExclude = await Existing("Ahorro de un tercero");

        await Opening(tracked, 500m);
        await Opening(toExclude, 300m);

        Assert.Equal(800m, (await NewDashboard().GetMonthlySummaryAsync(Today)).TrackedBalance);

        var updated = await _registrar.SetTrackedAsync(
            toExclude.PublicId, new SetAccountTrackedRequest(false));

        Assert.False(updated.IsTracked);
        Assert.Equal(500m, (await NewDashboard().GetMonthlySummaryAsync(Today)).TrackedBalance);
    }

    // RF-2: the projection does not exist yet (épica E09), so there is nothing to assert here.
    // Documented as pending in progress/impl_004.md instead of faked with a test.

    // RF-3
    [Fact(DisplayName = "An uncontrolled account still admits movements")]
    public async Task an_uncontrolled_account_still_admits_movements()
    {
        var account = await Existing("Ahorro de un tercero");
        await Opening(account, 300m);

        await _registrar.SetTrackedAsync(account.PublicId, new SetAccountTrackedRequest(false));

        await Movement(account, 40m, Today);

        Assert.Equal(260m, await BalanceOf(account));
    }

    // RF-4
    [Fact(DisplayName = "An uncontrolled account still shows its own balance")]
    public async Task an_uncontrolled_account_still_shows_its_own_balance()
    {
        var account = await Existing("Ahorro de un tercero");
        await Opening(account, 300m);

        await _registrar.SetTrackedAsync(account.PublicId, new SetAccountTrackedRequest(false));

        var directory = new AccountDirectory(_database.Context, new TestTenantContext(HouseholdId));
        var summary = Assert.Single(
            await directory.ListRealAccountsAsync(), a => a.Id == account.PublicId);

        Assert.False(summary.IsTracked);
        Assert.Equal(300m, summary.Balance);
    }

    // RF-5
    [Fact(DisplayName = "Fixing a minimum buffer saves it as the account's alert threshold")]
    public async Task fixing_a_minimum_buffer_saves_it_as_the_accounts_alert_threshold()
    {
        var account = await Existing("Santander conjunta");

        var updated = await _registrar.SetMinimumBufferTargetAsync(
            account.PublicId, new SetMinimumBufferTargetRequest(150m));

        Assert.Equal(150m, updated.MinimumBufferTarget);

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.Equal(150m, stored.MinimumBufferTarget);
    }

    // RF-6
    [Fact(DisplayName = "A minimum buffer of zero is accepted")]
    public async Task a_minimum_buffer_of_zero_is_accepted()
    {
        var account = await Existing("Santander conjunta");

        var updated = await _registrar.SetMinimumBufferTargetAsync(
            account.PublicId, new SetMinimumBufferTargetRequest(0m));

        Assert.Equal(0m, updated.MinimumBufferTarget);
    }

    // RF-7
    [Fact(DisplayName = "A negative minimum buffer is refused")]
    public async Task a_negative_minimum_buffer_is_refused()
    {
        var account = await Existing("Santander conjunta");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.SetMinimumBufferTargetAsync(
                account.PublicId, new SetMinimumBufferTargetRequest(-1m)));

        Assert.Equal(
            "El colchón mínimo no puede ser negativo.", Assert.Single(error.Errors["amount"]));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.Null(stored.MinimumBufferTarget);
    }

    // RF-8: the buffer is kept while the account is uncontrolled, ready for the alert engine that
    // historia 037 builds. Emitting the alert itself is out of this story's scope.
    [Fact(DisplayName = "A minimum buffer survives marking the account as not controlled")]
    public async Task a_minimum_buffer_survives_marking_the_account_as_not_controlled()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.SetMinimumBufferTargetAsync(
            account.PublicId, new SetMinimumBufferTargetRequest(150m));

        var updated = await _registrar.SetTrackedAsync(
            account.PublicId, new SetAccountTrackedRequest(false));

        Assert.False(updated.IsTracked);
        Assert.Equal(150m, updated.MinimumBufferTarget);
    }

    // RF-9
    [Fact(DisplayName = "Recontrolling an account reincorporates its whole balance, not only what happens next")]
    public async Task recontrolling_an_account_reincorporates_its_whole_balance()
    {
        var account = await Existing("Ahorro de un tercero");
        await Opening(account, 300m);

        await _registrar.SetTrackedAsync(account.PublicId, new SetAccountTrackedRequest(false));
        Assert.Equal(0m, (await NewDashboard().GetMonthlySummaryAsync(Today)).TrackedBalance);

        var updated = await _registrar.SetTrackedAsync(
            account.PublicId, new SetAccountTrackedRequest(true));

        Assert.True(updated.IsTracked);
        Assert.Equal(300m, (await NewDashboard().GetMonthlySummaryAsync(Today)).TrackedBalance);
    }

    // RF-10
    [Fact(DisplayName = "The spend-by-category report excludes movements of an uncontrolled account")]
    public async Task the_spend_by_category_report_excludes_movements_of_an_uncontrolled_account()
    {
        var tracked = await Existing("Santander conjunta");
        var untracked = await Existing("Ahorro de un tercero");

        await Opening(tracked, 500m);
        await Opening(untracked, 500m);

        await _registrar.SetTrackedAsync(untracked.PublicId, new SetAccountTrackedRequest(false));

        await Movement(tracked, 40m, Today);
        await Movement(untracked, 25m, Today);

        var summary = await NewDashboard().GetMonthlySummaryAsync(Today);

        var total = summary.ByCategory.Sum(c => c.Total);
        Assert.Equal(40m, total);
    }

    // RF-11
    [Fact(DisplayName = "A minimum buffer on a credit card is refused")]
    public async Task a_minimum_buffer_on_a_credit_card_is_refused()
    {
        var creditCard = await Existing("Tarjeta Santander", AccountType.CreditCard);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.SetMinimumBufferTargetAsync(
                creditCard.PublicId, new SetMinimumBufferTargetRequest(100m)));

        Assert.Equal(
            "Una tarjeta de crédito no admite un colchón mínimo: su saldo es deuda, no liquidez.",
            Assert.Single(error.Errors["amount"]));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == creditCard.Id);
        Assert.Null(stored.MinimumBufferTarget);
    }

    // RF-12
    [Fact(DisplayName = "Marking a credit card as not controlled is refused")]
    public async Task marking_a_credit_card_as_not_controlled_is_refused()
    {
        var creditCard = await Existing("Tarjeta Santander", AccountType.CreditCard);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.SetTrackedAsync(
                creditCard.PublicId, new SetAccountTrackedRequest(false)));

        Assert.Equal(
            "Una tarjeta de crédito no puede excluirse del disponible: su saldo es deuda, no "
                + "dinero disponible.",
            Assert.Single(error.Errors["isTracked"]));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == creditCard.Id);
        Assert.True(stored.IsTracked);
    }

    // RF-13
    [Fact(DisplayName = "Retiring a minimum buffer clears the account's alert threshold")]
    public async Task retiring_a_minimum_buffer_clears_the_accounts_alert_threshold()
    {
        var account = await Existing("Santander conjunta");

        await _registrar.SetMinimumBufferTargetAsync(
            account.PublicId, new SetMinimumBufferTargetRequest(150m));

        var updated = await _registrar.SetMinimumBufferTargetAsync(
            account.PublicId, new SetMinimumBufferTargetRequest(null));

        Assert.Null(updated.MinimumBufferTarget);

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.Null(stored.MinimumBufferTarget);
    }

    // RF-14
    [Fact(DisplayName = "Any member of the household may change control or the minimum buffer")]
    public async Task any_member_of_the_household_may_change_control_or_the_minimum_buffer()
    {
        var account = await Existing("Santander conjunta");

        var byOneMember = await NewRegistrar(memberId: 7).SetTrackedAsync(
            account.PublicId, new SetAccountTrackedRequest(false));
        Assert.False(byOneMember.IsTracked);

        var byAnother = await NewRegistrar(memberId: 42).SetMinimumBufferTargetAsync(
            account.PublicId, new SetMinimumBufferTargetRequest(200m));
        Assert.Equal(200m, byAnother.MinimumBufferTarget);

        var byAnonymous = await NewRegistrar(memberId: null).SetTrackedAsync(
            account.PublicId, new SetAccountTrackedRequest(true));
        Assert.True(byAnonymous.IsTracked);
    }

    // An account of another household is not this household's to change anything about.
    [Fact(DisplayName = "An account of another household is not available")]
    public async Task an_account_of_another_household_is_not_available()
    {
        var account = await Existing("Santander conjunta", householdId: HouseholdId + 1);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.SetTrackedAsync(
                account.PublicId, new SetAccountTrackedRequest(false)));

        Assert.Equal("Esa cuenta no está disponible.", Assert.Single(error.Errors["accountId"]));
    }

    private AccountControlRegistrar NewRegistrar(int? memberId = null) =>
        new(_database.Context, new TestTenantContext(HouseholdId, memberId));

    private DashboardQuery NewDashboard() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
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

    private async Task Opening(Account account, decimal amount)
    {
        _database.Context.Entries.Add(JournalEntry.OpenBalance(
            account.HouseholdId, account, Euros(amount), Today.AddDays(-30)));

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>
    /// An expense recorded against the account, so tests about movements have something to spend.
    /// </summary>
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

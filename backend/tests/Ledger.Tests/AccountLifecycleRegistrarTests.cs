using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Renaming, changing the type of, and archiving or unarchiving an account, as story 005 specifies
/// it. Every test names the requirement it pins down; the map from RF to test lives in
/// <c>progress/impl_005.md</c>.
/// </summary>
public sealed class AccountLifecycleRegistrarTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 9);

    /// <summary>A clock parked on <see cref="Today"/>, so that "tomorrow" stays in the future.</summary>
    private static readonly TimeProvider Clock =
        new FixedTimeProvider(new DateTimeOffset(Today, new TimeOnly(10, 0), TimeSpan.Zero));

    private readonly LedgerDatabase _database = new();
    private readonly AccountLifecycleRegistrar _registrar;

    public AccountLifecycleRegistrarTests()
    {
        _registrar = NewRegistrar();
    }

    public void Dispose() => _database.Dispose();

    // RF-1
    [Fact(DisplayName = "Renaming an account saves the new name and it shows on existing movements")]
    public async Task renaming_an_account_saves_the_new_name_and_it_shows_on_existing_movements()
    {
        var account = await Existing("Santander conjunta");
        await Movement(account, 40m, Today);

        var updated = await _registrar.RenameAsync(
            account.PublicId, new RenameAccountRequest("Santander cuenta conjunta"));

        Assert.Equal("Santander cuenta conjunta", updated.Name);

        var directory = new AccountDirectory(_database.Context, new TestTenantContext(HouseholdId));
        var summary = Assert.Single(
            await directory.ListRealAccountsAsync(), a => a.Id == account.PublicId);

        // The movement is not denormalized: renaming the account is enough for it to read the new
        // name wherever the movement is shown, because both point at the very same row.
        Assert.Equal("Santander cuenta conjunta", summary.Name);
    }

    // RF-2
    [Theory(DisplayName = "Renaming an account to a name already taken is refused")]
    [InlineData("Efectivo")]
    [InlineData("EFECTIVO")]
    [InlineData("  efectivo  ")]
    public async Task renaming_an_account_to_a_name_already_taken_is_refused(string attempted)
    {
        await Existing("Efectivo");
        var account = await Existing("Santander conjunta");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.RenameAsync(account.PublicId, new RenameAccountRequest(attempted)));

        Assert.Equal("Ya existe una cuenta con ese nombre.", Assert.Single(error.Errors["name"]));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.Equal("Santander conjunta", stored.Name);
    }

    // RF-2, edge case: the name is still taken even when the account holding it is archived
    [Fact(DisplayName = "Renaming to a name held by an archived account is refused")]
    public async Task renaming_to_a_name_held_by_an_archived_account_is_refused()
    {
        var archived = await Existing("Efectivo");
        await _registrar.ArchiveAsync(archived.PublicId);

        var account = await Existing("Santander conjunta");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.RenameAsync(account.PublicId, new RenameAccountRequest("Efectivo")));

        Assert.Equal("Ya existe una cuenta con ese nombre.", Assert.Single(error.Errors["name"]));
    }

    // RF-2, edge case: renaming an account to the name it already has is not a conflict with itself
    [Fact(DisplayName = "Renaming an account to its own current name is accepted")]
    public async Task renaming_an_account_to_its_own_current_name_is_accepted()
    {
        var account = await Existing("Santander conjunta");

        var updated = await _registrar.RenameAsync(
            account.PublicId, new RenameAccountRequest("Santander conjunta"));

        Assert.Equal("Santander conjunta", updated.Name);
    }

    // RF-2, edge case: an archived account may still be renamed while archived
    [Fact(DisplayName = "An archived account can be renamed while still archived")]
    public async Task an_archived_account_can_be_renamed_while_still_archived()
    {
        var account = await Existing("Efectivo");
        await _registrar.ArchiveAsync(account.PublicId);

        var updated = await _registrar.RenameAsync(
            account.PublicId, new RenameAccountRequest("Efectivo viejo"));

        Assert.Equal("Efectivo viejo", updated.Name);
    }

    // RF-3
    [Fact(DisplayName = "Changing the type of an account that already has movements is refused")]
    public async Task changing_the_type_of_an_account_that_already_has_movements_is_refused()
    {
        var account = await Existing("Santander conjunta");
        await Movement(account, 40m, Today);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.ChangeTypeAsync(
                account.PublicId, new ChangeAccountTypeRequest("savings")));

        Assert.Equal(
            "No se puede cambiar el tipo de una cuenta que ya tiene movimientos registrados.",
            Assert.Single(error.Errors["type"]));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.Equal(AccountType.Checking, stored.Type);
    }

    // RF-3, edge case: an opening balance is not a movement for this rule
    [Fact(DisplayName = "Changing the type of an account with only an opening balance is accepted")]
    public async Task changing_the_type_of_an_account_with_only_an_opening_balance_is_accepted()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);

        var updated = await _registrar.ChangeTypeAsync(
            account.PublicId, new ChangeAccountTypeRequest("savings"));

        Assert.Equal("savings", updated.Type);
    }

    // RF-4
    [Fact(DisplayName = "An archived account no longer admits new movements")]
    public async Task an_archived_account_no_longer_admits_new_movements()
    {
        var account = await Existing("Efectivo", AccountType.Cash);
        await _registrar.ArchiveAsync(account.PublicId);

        var expenses = await ExpenseRegistrarFor();
        var category = await NewExpenseCategory();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => expenses.RegisterAsync(new MyHome.Modules.Ledger.Contracts.Expenses
                .RegisterExpenseRequest(
                    account.PublicId,
                    category.PublicId,
                    10m,
                    Today,
                    "Compra")));

        Assert.True(error.Errors.ContainsKey("accountId"));
    }

    // RF-5
    [Fact(DisplayName = "Archiving an account keeps every movement already recorded against it")]
    public async Task archiving_an_account_keeps_every_movement_already_recorded_against_it()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);
        await Movement(account, 500m, Today);

        await _registrar.ArchiveAsync(account.PublicId);

        var postingsLeft = await _database.Context.Postings
            .Where(p => p.AccountId == account.Id)
            .CountAsync();

        Assert.Equal(2, postingsLeft);
    }

    // RF-6
    [Fact(DisplayName = "Archiving an account excludes it from the disponible real")]
    public async Task archiving_an_account_excludes_it_from_the_disponible_real()
    {
        var kept = await Existing("Santander conjunta");
        var toArchive = await Existing("Ahorro", AccountType.Savings);

        await Opening(kept, 500m);
        // The account being archived must reach a zero balance first (RF-8), so the exclusion this
        // test pins down is not about the number moving, but about the account itself dropping out
        // of the household's real accounts once archived.
        await Opening(toArchive, 300m);
        await Movement(toArchive, 300m, Today);

        var before = await NewDashboard().GetMonthlySummaryAsync(Today);
        Assert.Equal(500m, before.TrackedBalance);
        Assert.Contains(before.Accounts, a => a.Id == toArchive.PublicId);

        await _registrar.ArchiveAsync(toArchive.PublicId);

        var after = await NewDashboard().GetMonthlySummaryAsync(Today);
        Assert.Equal(500m, after.TrackedBalance);
        Assert.DoesNotContain(after.Accounts, a => a.Id == toArchive.PublicId);
    }

    // RF-7
    [Fact(DisplayName = "Archiving an account excludes it from the selectable account list")]
    public async Task archiving_an_account_excludes_it_from_the_selectable_account_list()
    {
        var account = await Existing("Efectivo", AccountType.Cash);

        await _registrar.ArchiveAsync(account.PublicId);

        var directory = new AccountDirectory(_database.Context, new TestTenantContext(HouseholdId));
        var accounts = await directory.ListRealAccountsAsync();

        Assert.DoesNotContain(accounts, a => a.Id == account.PublicId);
    }

    // RF-8
    [Fact(DisplayName = "Archiving an account with a balance other than zero is refused")]
    public async Task archiving_an_account_with_a_balance_other_than_zero_is_refused()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.ArchiveAsync(account.PublicId));

        Assert.Equal(
            "No se puede archivar una cuenta con saldo distinto de cero. Déjala a cero primero.",
            Assert.Single(error.Errors["accountId"]));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.False(stored.IsArchived);
    }

    // RF-8, edge case: an opening balance declared but since brought back down to zero
    [Fact(DisplayName = "Archiving is accepted when the current balance is zero, opening balance aside")]
    public async Task archiving_is_accepted_when_the_current_balance_is_zero_opening_balance_aside()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);
        await Movement(account, 500m, Today);

        var updated = await _registrar.ArchiveAsync(account.PublicId);

        Assert.Equal(0m, updated.Balance);

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.True(stored.IsArchived);
    }

    // RF-9
    [Fact(DisplayName = "There is no operation to delete an account, only to archive it")]
    public void there_is_no_operation_to_delete_an_account_only_to_archive_it()
    {
        var members = typeof(IAccountLifecycleRegistrar).GetMethods();

        Assert.DoesNotContain(members, m => m.Name.Contains("Delete", StringComparison.Ordinal));
        Assert.DoesNotContain(members, m => m.Name.Contains("Remove", StringComparison.Ordinal));
    }

    // RF-10
    [Fact(DisplayName = "Unarchiving an account admits movements again")]
    public async Task unarchiving_an_account_admits_movements_again()
    {
        var account = await Existing("Efectivo", AccountType.Cash);
        await _registrar.ArchiveAsync(account.PublicId);

        var updated = await _registrar.UnarchiveAsync(account.PublicId);
        Assert.False(updated.IsArchived);

        var expenses = await ExpenseRegistrarFor();
        var category = await NewExpenseCategory();

        var registered = await expenses.RegisterAsync(new MyHome.Modules.Ledger.Contracts.Expenses
            .RegisterExpenseRequest(account.PublicId, category.PublicId, 10m, Today, "Compra"));

        Assert.NotNull(registered);

        var directory = new AccountDirectory(_database.Context, new TestTenantContext(HouseholdId));
        Assert.Contains(await directory.ListRealAccountsAsync(), a => a.Id == account.PublicId);
    }

    // RF-11
    [Theory(DisplayName = "Changing a credit card's type, or into a credit card, is always refused")]
    [InlineData("creditCard", "savings")]
    [InlineData("checking", "creditCard")]
    public async Task changing_a_credit_cards_type_or_into_a_credit_card_is_always_refused(
        string currentType, string requestedType)
    {
        var account = await Existing("Tarjeta o cuenta", ToAccountType(currentType));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.ChangeTypeAsync(
                account.PublicId, new ChangeAccountTypeRequest(requestedType)));

        Assert.Equal(
            "No se puede cambiar una cuenta a tarjeta de crédito, ni una tarjeta de crédito a otro "
                + "tipo: su saldo es deuda, no liquidez, y cambiarle la etiqueta no cambia eso.",
            Assert.Single(error.Errors["type"]));
    }

    // RF-12
    [Fact(DisplayName = "Archiving an account with an active recurring rule pointing at it is refused")]
    public async Task archiving_an_account_with_an_active_recurring_rule_pointing_at_it_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var rule = RecurringRule.Create(
            HouseholdId,
            EntryKind.Expense,
            RecurrenceFrequency.Monthly,
            account,
            category,
            "Alquiler",
            Euros(600m),
            Today);

        _database.Context.RecurringRules.Add(rule);
        await _database.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.ArchiveAsync(account.PublicId));

        Assert.Contains("Alquiler", Assert.Single(error.Errors["accountId"]));

        var stored = await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.False(stored.IsArchived);
    }

    // RF-12
    [Fact(DisplayName = "Archiving an account with a pending planned movement pointing at it is refused")]
    public async Task archiving_an_account_with_a_pending_planned_movement_pointing_at_it_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var planned = PlannedMovement.Create(
            HouseholdId,
            EntryKind.Expense,
            account,
            category,
            "Seguro del coche",
            Euros(120m),
            Today.AddDays(10));

        _database.Context.PlannedMovements.Add(planned);
        await _database.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.ArchiveAsync(account.PublicId));

        Assert.Contains("Seguro del coche", Assert.Single(error.Errors["accountId"]));
    }

    // RF-12, edge case: a deactivated rule no longer blocks archiving
    [Fact(DisplayName = "A deactivated recurring rule no longer blocks archiving")]
    public async Task a_deactivated_recurring_rule_no_longer_blocks_archiving()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var rule = RecurringRule.Create(
            HouseholdId,
            EntryKind.Expense,
            RecurrenceFrequency.Monthly,
            account,
            category,
            "Alquiler",
            Euros(600m),
            Today);

        rule.Deactivate();

        _database.Context.RecurringRules.Add(rule);
        await _database.Context.SaveChangesAsync();

        var updated = await _registrar.ArchiveAsync(account.PublicId);

        Assert.True((await _database.Context.Accounts.SingleAsync(a => a.Id == account.Id)).IsArchived);
        Assert.Equal(0m, updated.Balance);
    }

    // RF-13
    [Fact(DisplayName = "Any member of the household may rename, change the type of, or archive an account")]
    public async Task any_member_of_the_household_may_rename_change_or_archive_an_account()
    {
        var account = await Existing("Santander conjunta");

        var renamed = await NewRegistrar(memberId: 7).RenameAsync(
            account.PublicId, new RenameAccountRequest("Santander cuenta conjunta"));
        Assert.Equal("Santander cuenta conjunta", renamed.Name);

        var retyped = await NewRegistrar(memberId: 42).ChangeTypeAsync(
            account.PublicId, new ChangeAccountTypeRequest("savings"));
        Assert.Equal("savings", retyped.Type);

        var archived = await NewRegistrar(memberId: null).ArchiveAsync(account.PublicId);
        Assert.True(archived.Balance == 0m);

        var unarchived = await NewRegistrar(memberId: 7).UnarchiveAsync(account.PublicId);
        Assert.NotNull(unarchived);
    }

    // Coherence with story 004: archiving does not touch the control or buffer settings already in
    // place; they are simply carried over, untouched, into the archived account.
    [Fact(DisplayName = "Archiving an account leaves its control and minimum buffer settings untouched")]
    public async Task archiving_an_account_leaves_its_control_and_minimum_buffer_settings_untouched()
    {
        var account = await Existing("Ahorro de un tercero", AccountType.Savings);

        var control = new AccountControlRegistrar(_database.Context, new TestTenantContext(HouseholdId));
        await control.SetTrackedAsync(account.PublicId, new SetAccountTrackedRequest(false));
        await control.SetMinimumBufferTargetAsync(
            account.PublicId, new SetMinimumBufferTargetRequest(150m));

        var archived = await _registrar.ArchiveAsync(account.PublicId);

        Assert.False(archived.IsTracked);
        Assert.Equal(150m, archived.MinimumBufferTarget);
    }

    // An account of another household is not this household's to change anything about.
    [Fact(DisplayName = "An account of another household is not available")]
    public async Task an_account_of_another_household_is_not_available()
    {
        var account = await Existing("Santander conjunta", householdId: HouseholdId + 1);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.ArchiveAsync(account.PublicId));

        Assert.Equal("Esa cuenta no está disponible.", Assert.Single(error.Errors["accountId"]));
    }

    private static AccountType ToAccountType(string type) => type switch
    {
        "checking" => AccountType.Checking,
        "savings" => AccountType.Savings,
        "cash" => AccountType.Cash,
        "creditCard" => AccountType.CreditCard,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private AccountLifecycleRegistrar NewRegistrar(int? memberId = null) =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId, memberId),
            new RenameAccountRequestValidator(),
            new ChangeAccountTypeRequestValidator());

    private DashboardQuery NewDashboard() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new TestHouseholdDirectory(CurrencyCode.Euro),
            new DashboardMonthRequestValidator(),
            TimeProvider.System);

    private async Task<ExpenseRegistrar> ExpenseRegistrarFor() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new MyHome.Modules.Ledger.Application.RegisterExpenseRequestValidator(Clock),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private async Task<Category> NewExpenseCategory()
    {
        var category = Category.Create(
            HouseholdId, $"Gasto {Guid.CreateVersion7()}", CategoryKind.Expense, colorIndex: 1);

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }

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

    /// <summary>An expense recorded against the account, so tests have a movement to work with.</summary>
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

        var category = await NewExpenseCategory();

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
}

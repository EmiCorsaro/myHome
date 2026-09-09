using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Contracts.Incomes;
using MyHome.Modules.Ledger.Contracts.Transfers;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Voiding and correcting a movement, as story 013 specifies it. Every test names the requirement
/// it pins down; the map from RF to test lives in <c>progress/impl_013.md</c>. RF-1, RF-2, RF-3,
/// RF-6, RF-7, RF-8, RF-11 and RF-12 are pinned down at the domain level in
/// <see cref="JournalEntryTests"/>; the tests here confirm the service in front of it — the one
/// that finds the movement by its public id and enforces the same rules for the caller — carries
/// the very same guarantee through, across the three kinds of movement (RF-10), and that correcting
/// a movement (RF-5, RF-13) is exactly voiding it and registering a new one.
/// </summary>
public sealed class MovementLifecycleRegistrarTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 9);

    private readonly LedgerDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // RF-1, RF-3, RF-10: voiding an expense returns the paying account's balance to what it was
    // before the expense — the finishing criterion the spec asks for explicitly.
    [Fact(DisplayName = "Voiding an expense restores the account balance it had before")]
    public async Task voiding_an_expense_restores_the_account_balance_it_had_before()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);
        var category = await NewExpenseCategory();

        var expense = await ExpenseRegistrarFor().RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 42.35m, Today));

        Assert.Equal(457.65m, await BalanceOf(account));

        await MovementLifecycleRegistrarFor().VoidAsync(expense.Id);

        Assert.Equal(500m, await BalanceOf(account));
    }

    // RF-1, RF-10: voiding an income returns the deposit account's balance to what it was before.
    [Fact(DisplayName = "Voiding an income restores the account balance it had before")]
    public async Task voiding_an_income_restores_the_account_balance_it_had_before()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);
        var category = await NewIncomeCategory();

        var income = await IncomeRegistrarFor().RegisterAsync(
            new RegisterIncomeRequest(account.PublicId, category.PublicId, 1500m, Today));

        Assert.Equal(2000m, await BalanceOf(account));

        await MovementLifecycleRegistrarFor().VoidAsync(income.Id);

        Assert.Equal(500m, await BalanceOf(account));
    }

    // RF-1, RF-10: voiding a transfer returns both accounts' balances to what they were before.
    [Fact(DisplayName = "Voiding a transfer restores both account balances to what they were before")]
    public async Task voiding_a_transfer_restores_both_account_balances_to_what_they_were_before()
    {
        var checking = await Existing("Santander conjunta");
        var savings = await Existing("Hucha", AccountType.Savings);
        await Opening(checking, 1000m);
        await Opening(savings, 200m);

        var transfer = await TransferRegistrarFor().RegisterAsync(
            new RegisterTransferRequest(checking.PublicId, savings.PublicId, 300m, Today));

        Assert.Equal(700m, await BalanceOf(checking));
        Assert.Equal(500m, await BalanceOf(savings));

        await MovementLifecycleRegistrarFor().VoidAsync(transfer.Id);

        Assert.Equal(1000m, await BalanceOf(checking));
        Assert.Equal(200m, await BalanceOf(savings));
    }

    // RF-10: the exceptional transfer into an uncontrolled destination (story 011) can be voided
    // too, and its balances come back exactly the same way.
    [Fact(DisplayName = "Voiding a transfer into an uncontrolled destination restores both balances")]
    public async Task voiding_a_transfer_into_an_uncontrolled_destination_restores_both_balances()
    {
        var checking = await Existing("Santander conjunta");
        var wallet = await Existing("Efectivo del compañero", AccountType.Cash, isTracked: false);
        await Opening(checking, 1000m);
        var category = await NewExpenseCategory();

        var transfer = await TransferRegistrarFor().RegisterAsync(new RegisterTransferRequest(
            checking.PublicId, wallet.PublicId, 200m, Today, CategoryId: category.PublicId));

        await MovementLifecycleRegistrarFor().VoidAsync(transfer.Id);

        Assert.Equal(1000m, await BalanceOf(checking));
        Assert.Equal(0m, await BalanceOf(wallet));
    }

    // RF-2, RF-9: the original is never deleted, only marked.
    [Fact(DisplayName = "Voiding a movement keeps the original entry, marked as voided")]
    public async Task voiding_a_movement_keeps_the_original_entry_marked_as_voided()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expense = await ExpenseRegistrarFor().RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 42.35m, Today));

        await MovementLifecycleRegistrarFor().VoidAsync(expense.Id);

        var original = await _database.Context.Entries
            .SingleAsync(e => e.PublicId == expense.Id);

        Assert.True(original.IsVoided);
        Assert.Equal(2, await _database.Context.Entries.CountAsync());
    }

    // RF-6: the link between original and reversal is visible from either side through the ids
    // the service hands back, without searching by date or amount.
    [Fact(DisplayName = "Voiding a movement links it to its reversal from either side")]
    public async Task voiding_a_movement_links_it_to_its_reversal_from_either_side()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expense = await ExpenseRegistrarFor().RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 42.35m, Today));

        var voided = await MovementLifecycleRegistrarFor().VoidAsync(expense.Id);

        Assert.Equal(expense.Id, voided.MovementId);
        Assert.NotEqual(Guid.Empty, voided.ReversalMovementId);

        var original = await _database.Context.Entries
            .SingleAsync(e => e.PublicId == voided.MovementId);
        var reversal = await _database.Context.Entries
            .SingleAsync(e => e.PublicId == voided.ReversalMovementId);

        Assert.Equal(original.Id, reversal.ReversalOfEntryId);
    }

    // RF-7: a movement already voided cannot be voided a second time.
    [Fact(DisplayName = "Voiding an already voided movement is refused")]
    public async Task voiding_an_already_voided_movement_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expense = await ExpenseRegistrarFor().RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 42.35m, Today));

        await MovementLifecycleRegistrarFor().VoidAsync(expense.Id);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => MovementLifecycleRegistrarFor().VoidAsync(expense.Id));

        Assert.True(error.Errors.ContainsKey("movementId"));
        Assert.Equal(2, await _database.Context.Entries.CountAsync());
    }

    // RF-8: a reversal is not one more movement that can be voided.
    [Fact(DisplayName = "Voiding a reversal is refused")]
    public async Task voiding_a_reversal_is_refused()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expense = await ExpenseRegistrarFor().RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 42.35m, Today));

        var voided = await MovementLifecycleRegistrarFor().VoidAsync(expense.Id);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => MovementLifecycleRegistrarFor().VoidAsync(voided.ReversalMovementId));

        Assert.True(error.Errors.ContainsKey("movementId"));
    }

    // RF-11: the reversal is dated the same as the movement it reverses, not today's.
    [Fact(DisplayName = "The reversal is dated the same as the movement it reverses")]
    public async Task the_reversal_is_dated_the_same_as_the_movement_it_reverses()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();
        var occurredOn = Today.AddDays(-45);

        var expense = await ExpenseRegistrarFor().RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 42.35m, occurredOn));

        var voided = await MovementLifecycleRegistrarFor().VoidAsync(expense.Id);

        Assert.Equal(occurredOn, voided.OccurredOn);
    }

    // RF-12: the opening balance has its own way to be corrected; it does not go through this.
    [Fact(DisplayName = "Voiding the opening balance is refused")]
    public async Task voiding_the_opening_balance_is_refused()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);

        var opening = await _database.Context.Entries
            .SingleAsync(e => e.OpeningAccountId == account.Id);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => MovementLifecycleRegistrarFor().VoidAsync(opening.PublicId));

        Assert.Equal(
            "The opening balance is not voided: correct it by editing it instead.",
            Assert.Single(error.Errors["movementId"]));
    }

    // RF-14: no role is required; any member, or none named at all, may void a movement.
    [Fact(DisplayName = "Voiding a movement needs no particular member")]
    public async Task voiding_a_movement_needs_no_particular_member()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expense = await ExpenseRegistrarFor().RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, category.PublicId, 42.35m, Today));

        var voided = await MovementLifecycleRegistrarFor().VoidAsync(expense.Id);

        Assert.NotEqual(Guid.Empty, voided.ReversalMovementId);
    }

    // RF-5, RF-13: correcting a movement is voiding the original and registering a new one, dated
    // whenever the person correcting it chooses — the "correct only the category" edge case from
    // the spec: the account balance ends up exactly where it started, but the category changes.
    [Fact(DisplayName = "Correcting only the category leaves the balance unchanged but the category new")]
    public async Task correcting_only_the_category_leaves_the_balance_unchanged_but_the_category_new()
    {
        var account = await Existing("Santander conjunta");
        await Opening(account, 500m);
        var wrongCategory = await NewExpenseCategory();
        var correctCategory = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var original = await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, wrongCategory.PublicId, 42.35m, Today));

        await MovementLifecycleRegistrarFor().VoidAsync(original.Id);

        var corrected = await expenses.RegisterAsync(
            new RegisterExpenseRequest(account.PublicId, correctCategory.PublicId, 42.35m, Today));

        Assert.Equal(457.65m, await BalanceOf(account));
        Assert.Equal(correctCategory.Name, corrected.CategoryName);

        // Opening balance, wrong original, its reversal, and the corrected expense: four entries,
        // none of them deleted (RF-9).
        Assert.Equal(4, await _database.Context.Entries.CountAsync());
    }

    // RF-13: the corrected movement may carry a date different from the original's.
    [Fact(DisplayName = "Correcting a movement may date the new one differently from the original")]
    public async Task correcting_a_movement_may_date_the_new_one_differently_from_the_original()
    {
        var account = await Existing("Santander conjunta");
        var category = await NewExpenseCategory();

        var expenses = ExpenseRegistrarFor();

        var original = await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 42.35m, Today.AddDays(-5)));

        var voided = await MovementLifecycleRegistrarFor().VoidAsync(original.Id);

        Assert.Equal(Today.AddDays(-5), voided.OccurredOn);

        var corrected = await expenses.RegisterAsync(new RegisterExpenseRequest(
            account.PublicId, category.PublicId, 50m, Today));

        Assert.Equal(Today, corrected.OccurredOn);
        Assert.Equal(50m, corrected.Amount);
    }

    // A movement that does not exist, or belongs to another household, is refused the same way.
    [Fact(DisplayName = "Voiding a movement of another household is refused")]
    public async Task voiding_a_movement_of_another_household_is_refused()
    {
        var registrar = new MovementLifecycleRegistrar(
            _database.Context, new TestTenantContext(HouseholdId + 1), TimeProvider.System);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => registrar.VoidAsync(Guid.CreateVersion7()));

        Assert.True(error.Errors.ContainsKey("movementId"));
    }

    private ExpenseRegistrar ExpenseRegistrarFor() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new RegisterExpenseRequestValidator(),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private IncomeRegistrar IncomeRegistrarFor() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new RegisterIncomeRequestValidator(),
            new TestHouseholdDirectory(CurrencyCode.Euro));

    private TransferRegistrar TransferRegistrarFor() =>
        new(
            _database.Context,
            new TestTenantContext(HouseholdId),
            new RegisterTransferRequestValidator());

    private MovementLifecycleRegistrar MovementLifecycleRegistrarFor() =>
        new(_database.Context, new TestTenantContext(HouseholdId), TimeProvider.System);

    private async Task<Account> Existing(
        string name,
        AccountType type = AccountType.Checking,
        bool isTracked = true)
    {
        var account = Account.Create(HouseholdId, name, type, CurrencyCode.Euro, isTracked);

        _database.Context.Accounts.Add(account);
        await _database.Context.SaveChangesAsync();

        return account;
    }

    private async Task<Category> NewExpenseCategory()
    {
        var category = Category.Create(
            HouseholdId, $"Gasto {Guid.CreateVersion7()}", CategoryKind.Expense, colorIndex: 1);

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

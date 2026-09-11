using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Budget;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// The edge case of story 051: two members of the household declare the same category and month at
/// the same time, both find it free, and only one line may survive (RF-6).
/// </summary>
/// <remarks>
/// The race is made deterministic instead of being hoped for: an interceptor writes the rival row
/// on the same connection in the gap between the duplicate check and the insert, which is exactly
/// the window the unique index exists to close.
/// </remarks>
public sealed class BudgetLineRaceTests
{
    private const string Message = "Esa categoría ya tiene una línea de presupuesto en ese mes.";

    private static readonly DateOnly ThisMonth = new(2026, 9, 1);

    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(new DateOnly(2026, 9, 20), new TimeOnly(10, 0), TimeSpan.Zero));

    [Fact(DisplayName = "The database refuses a second line for the same category and month")]
    public async Task the_database_refuses_a_second_line_for_the_same_category_and_month()
    {
        using var database = new LedgerDatabase();

        var (account, category) = await SeedAsync(database);

        database.Context.BudgetLines.Add(BudgetLine.Declare(
            HouseholdId, BudgetLineSign.Expense, category, account, ThisMonth, 300m));
        await database.Context.SaveChangesAsync();

        database.Context.BudgetLines.Add(BudgetLine.Declare(
            HouseholdId, BudgetLineSign.Expense, category, account, ThisMonth, 400m));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact(DisplayName = "Two households may each declare the same category for the same month")]
    public async Task two_households_may_each_declare_the_same_category_for_the_same_month()
    {
        using var database = new LedgerDatabase();

        var (account, category) = await SeedAsync(database);

        var foreignAccount = Account.Create(
            HouseholdId + 1, "Their bank", AccountType.Checking, CurrencyCode.Euro);
        var foreignCategory = Category.Create(
            HouseholdId + 1, "Supermercado", CategoryKind.Expense, 2);

        database.Context.Accounts.Add(foreignAccount);
        database.Context.Categories.Add(foreignCategory);
        await database.Context.SaveChangesAsync();

        database.Context.BudgetLines.Add(BudgetLine.Declare(
            HouseholdId, BudgetLineSign.Expense, category, account, ThisMonth, 300m));
        database.Context.BudgetLines.Add(BudgetLine.Declare(
            HouseholdId + 1,
            BudgetLineSign.Expense,
            foreignCategory,
            foreignAccount,
            ThisMonth,
            300m));

        await database.Context.SaveChangesAsync();

        Assert.Equal(2, await database.Context.BudgetLines.CountAsync());
    }

    [Fact(DisplayName = "Losing the race gives the duplicate error, not a failed request")]
    public async Task losing_the_race_gives_the_duplicate_error_not_a_failed_request()
    {
        var rival = new RivalDeclarer();

        using var database = new LedgerDatabase(rival);

        var (account, category) = await SeedAsync(database);

        // From here on the rival declares the same category and month in the gap the race needs.
        rival.Arm(HouseholdId, category.Id, account.Id, ThisMonth, 555m);

        var registrar = new BudgetLineRegistrar(
            database.Context,
            new TestTenantContext(HouseholdId),
            new DeclareBudgetLineRequestValidator(Clock));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => registrar.DeclareAsync(new DeclareBudgetLineRequest(
                category.PublicId, account.PublicId, ThisMonth, 300m)));

        Assert.Equal(Message, Assert.Single(error.Errors["categoryId"]));

        // Only the winner is there: the loser's row was refused, not written.
        var amounts = await database.Context.BudgetLines.Select(b => b.Amount).ToListAsync();
        Assert.Equal([555m], amounts);
    }

    private static async Task<(Account Account, Category Category)> SeedAsync(
        LedgerDatabase database)
    {
        var account = Account.Create(
            HouseholdId, "Cuenta conjunta", AccountType.Checking, CurrencyCode.Euro);
        var category = Category.Create(HouseholdId, "Supermercado", CategoryKind.Expense, 2);

        database.Context.Accounts.Add(account);
        database.Context.Categories.Add(category);
        await database.Context.SaveChangesAsync();

        return (account, category);
    }

    /// <summary>
    /// Another member of the household, declaring the same line in the gap the race needs.
    /// </summary>
    /// <remarks>
    /// Raw SQL on the same connection, once: going through a second <c>DbContext</c> would need a
    /// second connection, and two connections to one in-memory SQLite database are two databases.
    /// It stays disarmed until the fixture has finished seeding, so the seeding saves do not
    /// consume the one shot.
    /// </remarks>
    private sealed class RivalDeclarer : SaveChangesInterceptor
    {
        private bool _armed;
        private bool _written;
        private int _householdId;
        private int _categoryId;
        private int _accountId;
        private DateOnly _month;
        private decimal _amount;

        public void Arm(int householdId, int categoryId, int accountId, DateOnly month, decimal amount)
        {
            _armed = true;
            _householdId = householdId;
            _categoryId = categoryId;
            _accountId = accountId;
            _month = month;
            _amount = amount;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventData);

            if (!_armed || _written || eventData.Context is null)
            {
                return result;
            }

            _written = true;

            await eventData.Context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO budget_lines
                      (id, public_id, household_id, sign, category_id, account_id, period_start,
                       amount, currency, amount_mode, created_at)
                    VALUES
                      (9001, {Guid.CreateVersion7()}, {_householdId}, 'Expense', {_categoryId},
                       {_accountId}, {_month}, {_amount}, 'EUR', 'Estimated',
                       {DateTimeOffset.UtcNow})
                    """,
                    cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// The edge case of story 002: two members of the household save the same account name at the same
/// time, both find it free, and only one row may survive.
/// </summary>
/// <remarks>
/// The race is made deterministic instead of being hoped for: an interceptor writes the rival row
/// on the same connection in the gap between the duplicate check and the insert, which is exactly
/// the window the unique index exists to close.
/// </remarks>
public sealed class AccountNameRaceTests
{
    private const string Message = "Ya existe una cuenta con ese nombre.";

    [Fact(DisplayName = "The database refuses a second account with the same name, whatever its case")]
    public async Task the_database_refuses_a_second_account_with_the_same_name_whatever_its_case()
    {
        using var database = new LedgerDatabase();

        database.Context.Accounts.Add(
            Account.Create(HouseholdId, "Efectivo", AccountType.Cash, CurrencyCode.Euro));
        await database.Context.SaveChangesAsync();

        database.Context.Accounts.Add(
            Account.Create(HouseholdId, "EFECTIVO", AccountType.Savings, CurrencyCode.Euro));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact(DisplayName = "Two households may each hold an account with the same name")]
    public async Task two_households_may_each_hold_an_account_with_the_same_name()
    {
        using var database = new LedgerDatabase();

        database.Context.Accounts.Add(
            Account.Create(HouseholdId, "Efectivo", AccountType.Cash, CurrencyCode.Euro));
        database.Context.Accounts.Add(
            Account.Create(HouseholdId + 1, "Efectivo", AccountType.Cash, CurrencyCode.Euro));

        await database.Context.SaveChangesAsync();

        Assert.Equal(2, await database.Context.Accounts.CountAsync());
    }

    [Fact(DisplayName = "The accounts the system keeps are outside the unique name index")]
    public async Task the_accounts_the_system_keeps_are_outside_the_unique_name_index()
    {
        // One per currency is how the ledger classifies expenses, and they all carry the same
        // name. The index must not stand in the way of rows the user never asked for.
        using var database = new LedgerDatabase();

        database.Context.Accounts.Add(
            Account.Create(HouseholdId, "Gastos", AccountType.Expense, CurrencyCode.Euro));
        database.Context.Accounts.Add(
            Account.Create(HouseholdId, "Gastos", AccountType.Expense, CurrencyCode.UsDollar));

        await database.Context.SaveChangesAsync();

        Assert.Equal(2, await database.Context.Accounts.CountAsync());
    }

    [Fact(DisplayName = "Losing the race for a name gives the duplicate error, not a failed request")]
    public async Task losing_the_race_for_a_name_gives_the_duplicate_error()
    {
        // The rival saves "EFECTIVO" once the registrar has already checked and found it free.
        using var database = new LedgerDatabase(new RivalWriter(HouseholdId, "EFECTIVO"));

        var registrar = new AccountRegistrar(
            database.Context,
            new TestTenantContext(HouseholdId),
            new CreateAccountRequestValidator(),
            new TestHouseholdDirectory(CurrencyCode.Euro));

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => registrar.CreateAsync(new CreateAccountRequest("Efectivo", "cash")));

        Assert.Equal(Message, Assert.Single(error.Errors["name"]));

        // Only the winner is there: the loser's row was refused, not written.
        var names = await database.Context.Accounts.Select(a => a.Name).ToListAsync();
        Assert.Equal(["EFECTIVO"], names);
    }

    /// <summary>
    /// Another member of the household, saving the same name in the gap the race needs.
    /// </summary>
    /// <param name="householdId">Household the rival account belongs to.</param>
    /// <param name="name">Name the rival takes.</param>
    /// <remarks>
    /// Raw SQL on the same connection, once: going through a second <c>DbContext</c> would need a
    /// second connection, and two connections to one in-memory SQLite database are two databases.
    /// </remarks>
    private sealed class RivalWriter(int householdId, string name) : SaveChangesInterceptor
    {
        private bool _written;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventData);

            if (_written || eventData.Context is null)
            {
                return result;
            }

            _written = true;

            await eventData.Context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO accounts
                      (id, public_id, household_id, name, type, currency, is_tracked,
                       display_order, is_archived, created_at)
                    VALUES
                      (9001, {Guid.CreateVersion7()}, {householdId}, {name}, 'Cash', 'EUR', 1,
                       0, 0, {DateTimeOffset.UtcNow})
                    """,
                    cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
    }
}

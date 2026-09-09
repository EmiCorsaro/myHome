using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Persistence;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Guards the duplicate account-name lookup against the provider the application really runs on.
/// </summary>
/// <remarks>
/// The behaviour of the lookup is tested over SQLite (see <see cref="AccountRegistrarTests"/>),
/// which is a relational provider but not the one in production. Translating a query needs no
/// connection, so PostgreSQL's own translation can be checked here without a database: this is
/// the test that would have caught <c>string.Equals(x, y, StringComparison.OrdinalIgnoreCase)</c>,
/// which compiles, passes against a fake, and throws on the first real request.
/// </remarks>
public sealed class AccountQueryTranslationTests
{
    [Fact(DisplayName = "The duplicate account-name lookup translates to SQL on PostgreSQL")]
    public void the_duplicate_account_name_lookup_translates_to_sql_on_postgresql()
    {
        using var context = new LedgerDbContext(
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql("Host=localhost;Database=never-connected")
                .Options);

        var sql = context.Accounts
            .Where(AccountRegistrar.NameAlreadyTaken(LedgerFixtures.HouseholdId, "Efectivo"))
            .ToQueryString();

        Assert.Contains("lower(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("household_id", sql, StringComparison.Ordinal);
    }
}

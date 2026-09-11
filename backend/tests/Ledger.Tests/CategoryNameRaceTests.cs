using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// The edge case of story 001: two members of the household save the same name at the same time,
/// both find it free, and only one row may survive.
/// </summary>
/// <remarks>
/// The race is made deterministic instead of being hoped for: an interceptor writes the rival row
/// on the same connection in the gap between the duplicate check and the insert, which is exactly
/// the window the unique index exists to close.
/// </remarks>
public sealed class CategoryNameRaceTests
{
    private const string Message = "Ya existe una categoría con ese nombre.";

    [Fact(DisplayName = "The database refuses a second category with the same name, whatever its case")]
    public async Task the_database_refuses_a_second_category_with_the_same_name_whatever_its_case()
    {
        using var database = new LedgerDatabase();

        database.Context.Categories.Add(
            Category.Create(HouseholdId, "Ocio", CategoryKind.Expense, 1));
        await database.Context.SaveChangesAsync();

        database.Context.Categories.Add(
            Category.Create(HouseholdId, "OCIO", CategoryKind.Income, 2));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact(DisplayName = "Two households may each hold a category with the same name")]
    public async Task two_households_may_each_hold_a_category_with_the_same_name()
    {
        using var database = new LedgerDatabase();

        database.Context.Categories.Add(
            Category.Create(HouseholdId, "Ocio", CategoryKind.Expense, 1));
        database.Context.Categories.Add(
            Category.Create(HouseholdId + 1, "Ocio", CategoryKind.Expense, 1));

        await database.Context.SaveChangesAsync();

        Assert.Equal(2, await database.Context.Categories.CountAsync());
    }

    [Fact(DisplayName = "Losing the race for a name gives the duplicate error, not a failed request")]
    public async Task losing_the_race_for_a_name_gives_the_duplicate_error()
    {
        // The rival saves "OCIO" once the registrar has already checked and found "Ocio" free.
        using var database = new LedgerDatabase(new RivalWriter(HouseholdId, "OCIO"));

        var registrar = new CategoryRegistrar(
            database.Context,
            new TestTenantContext(HouseholdId),
            new CreateCategoryRequestValidator());

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => registrar.CreateAsync(new CreateCategoryRequest("Ocio", "expense")));

        Assert.Equal(Message, Assert.Single(error.Errors["name"]));

        // Only the winner is there: the loser's row was refused, not written.
        var names = await database.Context.Categories.Select(c => c.Name).ToListAsync();
        Assert.Equal(["OCIO"], names);
    }

    /// <summary>
    /// Another member of the household, saving the same name in the gap the race needs.
    /// </summary>
    /// <param name="householdId">Household the rival category belongs to.</param>
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
                    INSERT INTO categories
                      (id, public_id, household_id, name, kind, color_index, display_order,
                       is_archived, created_at)
                    VALUES
                      (9001, {Guid.CreateVersion7()}, {householdId}, {name}, 'Expense', 1, 0, 0,
                       {DateTimeOffset.UtcNow})
                    """,
                    cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
    }
}

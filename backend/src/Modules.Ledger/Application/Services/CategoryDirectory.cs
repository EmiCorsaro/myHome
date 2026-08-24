using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Lists the household's categories.
/// </summary>
/// <param name="db">Ledger data context.</param>
/// <param name="tenant">The current request's household.</param>
internal sealed class CategoryDirectory(LedgerDbContext db, ITenantContext tenant)
    : ICategoryDirectory
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CategorySummary>> ListExpenseCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();

        var categories = await db.Categories
            .Where(c => c.HouseholdId == householdId
                && !c.IsArchived
                && c.Kind == CategoryKind.Expense)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. categories.Select(ToSummary)];
    }

    /// <summary>
    /// Maps a category to the published contract.
    /// </summary>
    /// <param name="category">Category to map.</param>
    /// <returns>The summary.</returns>
    internal static CategorySummary ToSummary(Category category) => new(
        category.Id,
        category.Name,
        category.Kind.ToContractName(),
        category.ColorIndex,
        category.ParentId);
}

using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

internal sealed class CategoryDirectory(LedgerDbContext db, ITenantContext tenant)
    : ICategoryDirectory
{
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

        var publicIds = categories.ToDictionary(c => c.Id, c => c.PublicId);

        return
        [
            .. categories.Select(c => ToSummary(
                c,
                c.ParentId is { } parentId ? publicIds.GetValueOrDefault(parentId) : null)),
        ];
    }

    internal static CategorySummary ToSummary(Category category, Guid? parentPublicId) => new(
        category.PublicId,
        category.Name,
        category.Kind.ToContractName(),
        category.ColorIndex,
        parentPublicId);
}

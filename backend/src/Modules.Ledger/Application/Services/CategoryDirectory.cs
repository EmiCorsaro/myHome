using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

internal sealed class CategoryDirectory(LedgerDbContext db, ITenantContext tenant)
    : ICategoryDirectory
{
    public Task<IReadOnlyList<CategorySummary>> ListExpenseCategoriesAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default) =>
        ListAsync(CategoryKind.Expense, includeArchived, cancellationToken);

    public Task<IReadOnlyList<CategorySummary>> ListIncomeCategoriesAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default) =>
        ListAsync(CategoryKind.Income, includeArchived, cancellationToken);

    internal static CategorySummary ToSummary(Category category, Guid? parentPublicId) => new(
        category.PublicId,
        category.Name,
        category.Kind.ToContractName(),
        category.ColorIndex,
        parentPublicId,
        category.IsArchived);

    /// <summary>
    /// Lists one kind of category of the current household as a tree.
    /// </summary>
    /// <param name="kind">Kind of category to list.</param>
    /// <param name="includeArchived">Whether archived categories are listed too.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// The whole kind is read, archived included, so a parent's public id can always be resolved
    /// even when the parent itself is not listed. The ordering is done here rather than in SQL:
    /// ignoring accents needs a folding the database collation does not guarantee, and a
    /// household's categories are a handful of rows.
    /// </remarks>
    private async Task<IReadOnlyList<CategorySummary>> ListAsync(
        CategoryKind kind,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var householdId = tenant.RequireHouseholdId();

        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.HouseholdId == householdId && c.Kind == kind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var publicIds = categories.ToDictionary(c => c.Id, c => c.PublicId);

        var visible = categories
            .Where(c => includeArchived || !c.IsArchived)
            .ToList();

        var visibleIds = visible.Select(c => c.Id).ToHashSet();

        var childrenByParent = visible
            .Where(c => c.ParentId is { } parentId && visibleIds.Contains(parentId))
            .ToLookup(c => c.ParentId!.Value);

        // A category whose parent is not listed stands at the top level, so that nothing visible
        // is lost from the tree. With cascading archival this does not happen; it only guards
        // against rows that break that rule.
        var topLevel = visible
            .Where(c => c.ParentId is not { } parentId || !visibleIds.Contains(parentId))
            .OrderBy(c => c, CategoryNameOrder.Instance);

        var tree = new List<CategorySummary>(visible.Count);

        foreach (var category in topLevel)
        {
            tree.Add(Summarise(category));

            tree.AddRange(childrenByParent[category.Id]
                .OrderBy(c => c, CategoryNameOrder.Instance)
                .Select(Summarise));
        }

        return tree;

        CategorySummary Summarise(Category category) => ToSummary(
            category,
            category.ParentId is { } parentId && publicIds.TryGetValue(parentId, out var parent)
                ? parent
                : null);
    }
}

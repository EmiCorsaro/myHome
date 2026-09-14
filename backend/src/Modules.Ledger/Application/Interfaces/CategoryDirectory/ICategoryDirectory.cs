using MyHome.Modules.Ledger.Contracts.Categories;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Reads the category tree of the current household (story 007).
/// </summary>
/// <remarks>
/// Each list holds a single kind and is ordered as a tree: the top-level categories in
/// alphabetical order, each one followed by its own subcategories in alphabetical order. The
/// alphabetical order ignores case and accents.
/// </remarks>
public interface ICategoryDirectory
{
    Task<IReadOnlyList<CategorySummary>> ListExpenseCategoriesAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategorySummary>> ListIncomeCategoriesAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);
}

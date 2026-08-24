using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Categories;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Reads the household's categories.
/// </summary>
/// <remarks>
/// Takes no household identifier: it comes from the request's tenant context inside the
/// implementation. See <see cref="LedgerModule"/>.
/// </remarks>
public interface ICategoryDirectory
{
    /// <summary>
    /// Lists the expense categories, in display order.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The categories, or an empty list if there are none.</returns>
    Task<IReadOnlyList<CategorySummary>> ListExpenseCategoriesAsync(
        CancellationToken cancellationToken = default);
}

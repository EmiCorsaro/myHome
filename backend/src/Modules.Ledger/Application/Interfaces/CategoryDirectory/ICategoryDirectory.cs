using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Categories;

namespace MyHome.Modules.Ledger.Application;

public interface ICategoryDirectory
{
    Task<IReadOnlyList<CategorySummary>> ListExpenseCategoriesAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategorySummary>> ListIncomeCategoriesAsync(
        CancellationToken cancellationToken = default);
}

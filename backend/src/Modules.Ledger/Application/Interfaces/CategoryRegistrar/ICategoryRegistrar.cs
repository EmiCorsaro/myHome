using MyHome.Modules.Ledger.Contracts.Categories;

namespace MyHome.Modules.Ledger.Application;

public interface ICategoryRegistrar
{
    Task<CategorySummary> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);
}

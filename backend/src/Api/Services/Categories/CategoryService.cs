using MyHome.Api.Interfaces.Categories;
using MyHome.Modules.Ledger.Application;

namespace MyHome.Api.Services.Categories;

internal sealed class CategoryService(ICategoryDirectory categories) : ICategoryService
{
    public async Task<IResult> GetExpenseCategoriesAsync(CancellationToken cancellationToken)
    {
        var result = await categories.ListExpenseCategoriesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }
}

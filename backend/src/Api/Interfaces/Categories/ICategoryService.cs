namespace MyHome.Api.Interfaces.Categories;

public interface ICategoryService
{
    Task<IResult> GetExpenseCategoriesAsync(CancellationToken cancellationToken);
}

namespace MyHome.Modules.Ledger.Contracts.Categories;

public sealed record CreateCategoryRequest(
    string Name,
    string Kind,
    Guid? ParentId = null);

namespace MyHome.Modules.Ledger.Contracts.Categories;

public sealed record CategorySummary(
    Guid Id,
    string Name,
    string Kind,
    int ColorIndex,
    Guid? ParentId);

namespace MyHome.Modules.Ledger.Contracts.Categories;

/// <summary>
/// A category as the household sees it.
/// </summary>
/// <param name="Id">Public id of the category.</param>
/// <param name="Name">Name of the category.</param>
/// <param name="Kind"><c>income</c> or <c>expense</c>.</param>
/// <param name="ColorIndex">Colour of the palette, 1 to 10.</param>
/// <param name="ParentId">Public id of the parent category, when it is a subcategory.</param>
/// <param name="IsArchived">
/// Whether the category is archived. Only ever true when the caller asked to see archived
/// categories too (story 007, RF-5).
/// </param>
public sealed record CategorySummary(
    Guid Id,
    string Name,
    string Kind,
    int ColorIndex,
    Guid? ParentId,
    bool IsArchived = false);

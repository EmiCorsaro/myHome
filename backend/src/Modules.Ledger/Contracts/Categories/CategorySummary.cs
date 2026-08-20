namespace MyHome.Modules.Ledger.Contracts.Categories;

/// <summary>
/// A category as the outside world sees it.
/// </summary>
/// <param name="Id">Category identifier.</param>
/// <param name="Name">Visible name.</param>
/// <param name="Kind">Whether it classifies <c>income</c> or <c>expense</c>.</param>
/// <param name="ColorIndex">
/// Tone from the expressive palette, 1 to 10, mapped by the frontend to <c>--color-cat-N</c>.
/// Decided here so the same category keeps its colour across screens and clients.
/// </param>
/// <param name="ParentId">Parent category, or <see langword="null"/> at the top level.</param>
public sealed record CategorySummary(
    Guid Id,
    string Name,
    string Kind,
    int ColorIndex,
    Guid? ParentId);

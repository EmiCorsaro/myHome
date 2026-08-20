namespace MyHome.Modules.Ledger.Domain;

/// <summary>
/// Whether a category classifies money coming in or money going out.
/// </summary>
public enum CategoryKind
{
    /// <summary>Classifies income: salary, rent received, refunds.</summary>
    Income = 1,

    /// <summary>Classifies expense: groceries, rent paid, insurance.</summary>
    Expense = 2,
}

/// <summary>
/// The label answering "what was this money for".
/// </summary>
/// <remarks>
/// <para>
/// Categories are a tree one level deep in practice: <c>parentId</c> exists so the report can
/// aggregate by parent while entries stay recorded against the specific leaf. Recording against
/// "Groceries" and reading "Food" as a total is the difference between a report that is precise
/// and one that is readable.
/// </para>
/// <para>
/// <see cref="ColorIndex"/> is stored, not derived. Deriving the colour from the position in a
/// list would mean that adding one category silently repaints every chart the household has
/// already learned to read.
/// </para>
/// </remarks>
public sealed class Category
{
    private Category(
        Guid id,
        Guid householdId,
        string name,
        CategoryKind kind,
        int colorIndex,
        int displayOrder)
    {
        Id = id;
        HouseholdId = householdId;
        Name = name;
        Kind = kind;
        ColorIndex = colorIndex;
        DisplayOrder = displayOrder;
    }

    /// <summary>Number of tones in the expressive palette, defined in <c>tokens.css</c>.</summary>
    public const int PaletteSize = 10;

    /// <summary>Category identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Household that defines the category.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>Name as it is shown.</summary>
    public string Name { get; private set; }

    /// <summary>Whether it classifies income or expense.</summary>
    public CategoryKind Kind { get; private set; }

    /// <summary>Parent category, or <see langword="null"/> if it is a top-level one.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Tone from the expressive palette, from 1 to <see cref="PaletteSize"/>. The frontend maps
    /// it to <c>--color-cat-N</c>.
    /// </summary>
    public int ColorIndex { get; private set; }

    /// <summary>Position in listings. Lower comes first.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Whether the category is archived.</summary>
    public bool IsArchived { get; private set; }

    /// <summary>
    /// Creates a category.
    /// </summary>
    /// <param name="householdId">Household that defines it.</param>
    /// <param name="name">Visible name. Required.</param>
    /// <param name="kind">Whether it classifies income or expense.</param>
    /// <param name="colorIndex">
    /// Tone from 1 to <see cref="PaletteSize"/>. Values outside the range wrap around, so a
    /// caller creating the eleventh category gets a valid colour instead of an exception.
    /// </param>
    /// <param name="displayOrder">Position in listings.</param>
    /// <param name="parentId">Parent category, if any.</param>
    /// <returns>The new category.</returns>
    /// <example>
    /// <code>
    /// var groceries = Category.Create(householdId, "Groceries", CategoryKind.Expense, 2, 10);
    /// </code>
    /// </example>
    public static Category Create(
        Guid householdId,
        string name,
        CategoryKind kind,
        int colorIndex,
        int displayOrder = 0,
        Guid? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var wrapped = (((colorIndex - 1) % PaletteSize) + PaletteSize) % PaletteSize + 1;

        return new Category(
            Guid.CreateVersion7(),
            householdId,
            name.Trim(),
            kind,
            wrapped,
            displayOrder)
        {
            ParentId = parentId,
        };
    }

    /// <summary>Renames the category.</summary>
    /// <param name="name">New name. Required.</param>
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>Archives the category. Existing entries keep pointing at it.</summary>
    public void Archive() => IsArchived = true;
}

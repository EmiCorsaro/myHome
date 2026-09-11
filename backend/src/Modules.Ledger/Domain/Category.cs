using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public enum CategoryKind
{
    Income = 1,
    Expense = 2,
}

public sealed class Category : AuditedTenantEntity
{
    private Category(
        Guid publicId,
        int householdId,
        string name,
        CategoryKind kind,
        int colorIndex,
        int displayOrder,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        HouseholdId = householdId;
        Name = name;
        Kind = kind;
        ColorIndex = colorIndex;
        DisplayOrder = displayOrder;
        CreatedAt = createdAt;
    }

    public const int PaletteSize = 10;

    public string Name { get; private set; }

    public CategoryKind Kind { get; private set; }

    public int? ParentId { get; private set; }

    public int ColorIndex { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsArchived { get; private set; }

    public static Category Create(
        int householdId,
        string name,
        CategoryKind kind,
        int colorIndex,
        int displayOrder = 0,
        int? parentId = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var wrapped = (((colorIndex - 1) % PaletteSize) + PaletteSize) % PaletteSize + 1;

        return new Category(
            Guid.CreateVersion7(),
            householdId,
            name.Trim(),
            kind,
            wrapped,
            displayOrder,
            createdAt ?? DateTimeOffset.UtcNow)
        {
            ParentId = parentId,
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Archive() => IsArchived = true;
}

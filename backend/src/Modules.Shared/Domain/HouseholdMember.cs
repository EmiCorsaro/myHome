namespace MyHome.Modules.Shared.Domain;

public sealed class HouseholdMember : TenantEntity
{
    private HouseholdMember(
        Guid publicId,
        string displayName,
        MemberRole role,
        int displayOrder,
        DateOnly joinedAt,
        Guid? userId)
    {
        PublicId = publicId;
        DisplayName = displayName;
        Role = role;
        DisplayOrder = displayOrder;
        JoinedAt = joinedAt;
        UserId = userId;
    }

    public Guid? UserId { get; private set; }

    public string DisplayName { get; private set; }

    public MemberRole Role { get; private set; }

    public int DisplayOrder { get; private set; }

    public DateOnly JoinedAt { get; private set; }

    internal static HouseholdMember Create(
        string displayName,
        MemberRole role,
        int displayOrder,
        DateOnly joinedAt,
        Guid? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new HouseholdMember(
            Guid.CreateVersion7(),
            displayName.Trim(),
            role,
            displayOrder,
            joinedAt,
            userId);
    }

    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    public void LinkUser(Guid userId) => UserId = userId;
}

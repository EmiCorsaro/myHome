namespace MyHome.Modules.Shared.Domain;

/// <summary>
/// A person inside a household.
/// </summary>
/// <remarks>
/// A member is not the same thing as a user. <see cref="UserId"/> is optional on purpose: a
/// household may track the finances of someone who never opens the application, and that person
/// still has to exist for income or expenses to be attributed to them.
/// </remarks>
public sealed class HouseholdMember
{
    private HouseholdMember(
        Guid id,
        Guid householdId,
        string displayName,
        MemberRole role,
        int displayOrder,
        DateOnly joinedAt,
        Guid? userId)
    {
        Id = id;
        HouseholdId = householdId;
        DisplayName = displayName;
        Role = role;
        DisplayOrder = displayOrder;
        JoinedAt = joinedAt;
        UserId = userId;
    }

    /// <summary>Member identifier. UUID v7: sortable by creation time.</summary>
    public Guid Id { get; private set; }

    /// <summary>The household this member belongs to.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>
    /// Linked sign-in account, or <c>null</c> if this member never opens the application.
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>Name shown in the interface.</summary>
    public string DisplayName { get; private set; }

    /// <summary>What this member is allowed to do in the household.</summary>
    public MemberRole Role { get; private set; }

    /// <summary>
    /// Display order. Beyond ordering lists, it breaks ties in splits: when a cent is left over,
    /// it goes to the member with the lowest number. Without a fixed rule, two runs of the same
    /// split could produce different results.
    /// </summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Date the member joined the household.</summary>
    public DateOnly JoinedAt { get; private set; }

    /// <summary>
    /// Adds a member to a household.
    /// </summary>
    /// <param name="householdId">Target household.</param>
    /// <param name="displayName">Visible name. Cannot be blank.</param>
    /// <param name="role">Role within the household.</param>
    /// <param name="displayOrder">Display order, and tie-breaker for splits.</param>
    /// <param name="joinedAt">Join date.</param>
    /// <param name="userId">Sign-in account, if they have one.</param>
    /// <returns>The created member.</returns>
    /// <exception cref="ArgumentException">If the display name is blank.</exception>
    /// <example>
    /// <code>
    /// var member = HouseholdMember.Create(
    ///     householdId: household.Id,
    ///     displayName: "Ana",
    ///     role: MemberRole.Owner,
    ///     displayOrder: 0,
    ///     joinedAt: DateOnly.FromDateTime(DateTime.UtcNow));
    /// </code>
    /// </example>
    public static HouseholdMember Create(
        Guid householdId,
        string displayName,
        MemberRole role,
        int displayOrder,
        DateOnly joinedAt,
        Guid? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new HouseholdMember(
            Guid.CreateVersion7(),
            householdId,
            displayName.Trim(),
            role,
            displayOrder,
            joinedAt,
            userId);
    }

    /// <summary>Changes the member's visible name.</summary>
    /// <param name="displayName">New name. Cannot be blank.</param>
    /// <exception cref="ArgumentException">If the name is blank.</exception>
    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    /// <summary>Links a sign-in account to this member.</summary>
    /// <param name="userId">Identifier of the sign-in account.</param>
    public void LinkUser(Guid userId) => UserId = userId;
}

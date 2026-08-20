namespace MyHome.Modules.Shared.Households;

/// <summary>
/// What a member is allowed to do inside their household.
/// </summary>
/// <remarks>
/// The roles are deliberately few. A household is not an organisation: it does not need
/// per-resource permissions, it needs to know who can invite and who can only look.
/// </remarks>
public enum MemberRole
{
    /// <summary>
    /// Can do everything, including inviting and removing members and deleting the household.
    /// Every household has at least one.
    /// </summary>
    Owner = 1,

    /// <summary>
    /// Can record and edit entries, accounts and rules. Does not manage members.
    /// </summary>
    Member = 2,

    /// <summary>
    /// Read only. Intended for an external adviser or a relative helping with the books.
    /// </summary>
    Viewer = 3,
}

namespace MyHome.Modules.Shared.Households;

/// <summary>
/// The household: the system's tenancy unit. Every piece of data belongs to exactly one.
/// </summary>
/// <remarks>
/// The isolation boundary between tenants: every query carries a household filter and the model
/// has no room for an orphan row.
/// <para>
/// Multi-tenancy is here from the first commit even with one real household. Adding it later means
/// rewriting every query; having it now costs a column and a filter.
/// </para>
/// </remarks>
public sealed class Household
{
    private readonly List<HouseholdMember> _members = [];

    private Household(
        Guid id,
        string name,
        CurrencyCode baseCurrency,
        string timeZoneId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        BaseCurrency = baseCurrency;
        TimeZoneId = timeZoneId;
        CreatedAt = createdAt;
    }

    /// <summary>Household identifier. UUID v7: sortable by creation time.</summary>
    public Guid Id { get; private set; }

    /// <summary>The household's name, as its members see it.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// The currency the household's totals are expressed in. Entries may be in other
    /// currencies, but every consolidated balance is converted to this one.
    /// </summary>
    public CurrencyCode BaseCurrency { get; private set; }

    /// <summary>
    /// IANA time zone, for example <c>Europe/Madrid</c>.
    /// </summary>
    /// <remarks>
    /// Not a presentation detail. It determines which day counts as "today" when materialising a
    /// recurrence or closing a period, and an offset mismatch can move an entry into a different
    /// month.
    /// </remarks>
    public string TimeZoneId { get; private set; }

    /// <summary>Creation instant, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The household's members, in display order.</summary>
    public IReadOnlyList<HouseholdMember> Members => _members;

    /// <summary>
    /// Creates a household together with its first owner.
    /// </summary>
    /// <param name="name">Household name. Cannot be blank.</param>
    /// <param name="ownerDisplayName">Visible name of the first member, who becomes the owner.</param>
    /// <param name="baseCurrency">Base currency. Euro if omitted.</param>
    /// <param name="timeZoneId">IANA time zone. <c>Europe/Madrid</c> if omitted.</param>
    /// <param name="createdAt">Creation instant. Now, in UTC, if omitted.</param>
    /// <param name="ownerUserId">The owner's sign-in account, if it already exists.</param>
    /// <returns>The household, with an owner member already inside it.</returns>
    /// <exception cref="ArgumentException">If the household name is blank.</exception>
    /// <remarks>
    /// A household with no owner would have nobody able to administer it, so that intermediate
    /// state does not exist: both are created together or neither is.
    /// </remarks>
    /// <example>
    /// <code>
    /// var household = Household.Create("Ana and Bruno", ownerDisplayName: "Ana");
    /// household.AddMember("Bruno", MemberRole.Member);
    /// </code>
    /// </example>
    public static Household Create(
        string name,
        string ownerDisplayName,
        CurrencyCode? baseCurrency = null,
        string timeZoneId = "Europe/Madrid",
        DateTimeOffset? createdAt = null,
        Guid? ownerUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var moment = createdAt ?? DateTimeOffset.UtcNow;

        var household = new Household(
            Guid.CreateVersion7(),
            name.Trim(),
            baseCurrency ?? CurrencyCode.Euro,
            timeZoneId,
            moment);

        household.AddMember(
            ownerDisplayName,
            MemberRole.Owner,
            DateOnly.FromDateTime(moment.UtcDateTime),
            ownerUserId);

        return household;
    }

    /// <summary>
    /// Adds a member to the household, at the end of the display order.
    /// </summary>
    /// <param name="displayName">Visible name. Cannot be blank.</param>
    /// <param name="role">Role within the household.</param>
    /// <param name="joinedAt">Join date. Today, in UTC, if omitted.</param>
    /// <param name="userId">Sign-in account, if they have one.</param>
    /// <returns>The created member.</returns>
    /// <exception cref="ArgumentException">If the display name is blank.</exception>
    public HouseholdMember AddMember(
        string displayName,
        MemberRole role,
        DateOnly? joinedAt = null,
        Guid? userId = null)
    {
        var member = HouseholdMember.Create(
            Id,
            displayName,
            role,
            _members.Count,
            joinedAt ?? DateOnly.FromDateTime(DateTime.UtcNow),
            userId);

        _members.Add(member);
        return member;
    }

    /// <summary>Renames the household.</summary>
    /// <param name="name">New name. Cannot be blank.</param>
    /// <exception cref="ArgumentException">If the name is blank.</exception>
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
}

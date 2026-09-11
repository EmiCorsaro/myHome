namespace MyHome.Modules.Shared.Domain;

public sealed class Household : Entity
{
    private readonly List<HouseholdMember> _members = [];

    private Household(
        Guid publicId,
        string name,
        CurrencyCode baseCurrency,
        string timeZoneId,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        Name = name;
        BaseCurrency = baseCurrency;
        TimeZoneId = timeZoneId;
        CreatedAt = createdAt;
    }

    public Guid PublicId { get; private set; }

    public string Name { get; private set; }

    public CurrencyCode BaseCurrency { get; private set; }

    public string TimeZoneId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<HouseholdMember> Members => _members;

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

    public HouseholdMember AddMember(
        string displayName,
        MemberRole role,
        DateOnly? joinedAt = null,
        Guid? userId = null)
    {
        var member = HouseholdMember.Create(
            displayName,
            role,
            _members.Count,
            joinedAt ?? DateOnly.FromDateTime(DateTime.UtcNow),
            userId);

        _members.Add(member);
        return member;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
}

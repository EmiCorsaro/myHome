using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public sealed class Account : AuditedTenantEntity
{
    private Account(
        Guid publicId,
        int householdId,
        string name,
        AccountType type,
        CurrencyCode currency,
        bool isTracked,
        int displayOrder,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        HouseholdId = householdId;
        Name = name;
        Type = type;
        Currency = currency;
        IsTracked = isTracked;
        DisplayOrder = displayOrder;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; }

    public AccountType Type { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public bool IsTracked { get; private set; }

    public decimal? MinimumBufferTarget { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsArchived { get; private set; }

    public bool IsReal => Type is AccountType.Checking
        or AccountType.Savings
        or AccountType.Cash
        or AccountType.CreditCard;

    public static Account Create(
        int householdId,
        string name,
        AccountType type,
        CurrencyCode currency,
        bool isTracked = true,
        int displayOrder = 0,
        decimal? minimumBufferTarget = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var account = new Account(
            Guid.CreateVersion7(),
            householdId,
            name.Trim(),
            type,
            currency,
            isTracked,
            displayOrder,
            createdAt ?? DateTimeOffset.UtcNow);

        if (!account.IsReal)
        {
            account.IsTracked = false;
        }

        account.MinimumBufferTarget = minimumBufferTarget;

        return account;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Archive() => IsArchived = true;
}

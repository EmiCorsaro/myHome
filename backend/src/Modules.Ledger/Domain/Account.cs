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

    /// <summary>
    /// Restores an archived account so it can record movements again (story 005, RF-10).
    /// </summary>
    /// <remarks>
    /// The account keeps every setting it had before it was archived — its minimum buffer and
    /// whether it is controlled are untouched, exactly as they were left (story 005 does not ask
    /// for either to be reset).
    /// </remarks>
    public void Unarchive() => IsArchived = false;

    /// <summary>
    /// Changes what kind of account this is (story 005, RF-3, RF-11).
    /// </summary>
    /// <param name="type">The type the account should become.</param>
    /// <remarks>
    /// A credit card's balance is debt, not liquidity, and turning debt into spare cash — or the
    /// other way around — by simply relabelling the account would corrupt every figure derived from
    /// it. That is why the transition is refused in both directions, whatever the account's history
    /// (RF-11). This is the invariant's last line of defense; the application layer turns the same
    /// rule, and the "no movements yet" rule that only applies to every other type (RF-3), into
    /// validation errors before either ever reaches here.
    /// </remarks>
    public void ChangeType(AccountType type)
    {
        if (Type == AccountType.CreditCard || type == AccountType.CreditCard)
        {
            throw new InvalidOperationException(
                $"'{Name}' cannot change to or from a credit card: its balance would stop "
                    + "being debt, or start being it, and neither can happen by relabelling it.");
        }

        Type = type;
    }

    /// <summary>
    /// Marks the account as controlled or not (story 004, RF-1, RF-2, RF-9, RF-12).
    /// </summary>
    /// <param name="isTracked">
    /// <c>true</c> to fold the account back into the disponible real and the projection;
    /// <c>false</c> to keep it out of both while it keeps recording its own movements.
    /// </param>
    /// <remarks>
    /// A credit card's balance is debt, not liquidity: excluding it from the disponible real would
    /// inflate what the household can really spend, so it can never stop being controlled (RF-12).
    /// This is the invariant's last line of defense; the application layer turns the same rule into
    /// a validation error before it ever reaches here.
    /// </remarks>
    public void SetTracked(bool isTracked)
    {
        if (!isTracked && Type == AccountType.CreditCard)
        {
            throw new InvalidOperationException(
                $"'{Name}' is a credit card: its balance is debt, so excluding it from the "
                    + "disponible real would inflate it instead of shrinking it.");
        }

        IsTracked = isTracked;
    }

    /// <summary>
    /// Fixes or retires the account's minimum buffer, the liquidity floor the household wants to
    /// be warned about (story 004, RF-5, RF-6, RF-7, RF-11, RF-13).
    /// </summary>
    /// <param name="minimumBufferTarget">
    /// The floor to alert below, zero included (RF-6); <c>null</c> retires it (RF-13).
    /// </param>
    /// <remarks>
    /// A credit card's balance is debt, not liquidity: there is no floor to fix on it (RF-11). This
    /// is the invariant's last line of defense; the application layer turns the same rules into
    /// validation errors before they ever reach here.
    /// </remarks>
    public void SetMinimumBufferTarget(decimal? minimumBufferTarget)
    {
        if (minimumBufferTarget < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumBufferTarget),
                minimumBufferTarget,
                "The minimum buffer cannot be negative.");
        }

        if (minimumBufferTarget is not null && Type == AccountType.CreditCard)
        {
            throw new InvalidOperationException(
                $"'{Name}' is a credit card: its balance is debt, not liquidity, so it has no "
                    + "floor to fix.");
        }

        MinimumBufferTarget = minimumBufferTarget;
    }
}

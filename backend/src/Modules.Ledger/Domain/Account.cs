using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

/// <summary>
/// A place where money sits, or the doorway it comes in and goes out through.
/// </summary>
/// <remarks>
/// No stored balance: it is always summed from the postings, so it cannot drift from them. One
/// aggregation per query, which is nothing at a few thousand entries a year. If that ever
/// changes, use a materialised view rather than a counter column.
/// </remarks>
public sealed class Account
{
    private Account(
        Guid id,
        Guid householdId,
        string name,
        AccountType type,
        CurrencyCode currency,
        bool isTracked,
        int displayOrder,
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        Name = name;
        Type = type;
        Currency = currency;
        IsTracked = isTracked;
        DisplayOrder = displayOrder;
        CreatedAt = createdAt;
    }

    /// <summary>Account identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Household that owns the account.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>Name as it is shown, for instance "Santander joint".</summary>
    public string Name { get; private set; }

    /// <summary>What kind of account it is. See <see cref="AccountType"/>.</summary>
    public AccountType Type { get; private set; }

    /// <summary>Currency of the account. Every posting on it must use this one.</summary>
    public CurrencyCode Currency { get; private set; }

    /// <summary>
    /// Whether the account takes part in balance projection.
    /// </summary>
    /// <remarks>
    /// Only the accounts worth forecasting. A shortfall on the account paying the direct debits
    /// matters; a personal spending account fluctuating is not news.
    /// </remarks>
    public bool IsTracked { get; private set; }

    /// <summary>
    /// Balance below which the account is considered at risk, in major currency units.
    /// <see langword="null"/> if no floor has been set.
    /// </summary>
    /// <remarks>
    /// Lets the projection say "below your buffer on the 14th" instead of leaving the reader to
    /// judge whether 412 € is enough.
    /// </remarks>
    public decimal? MinimumBufferTarget { get; private set; }

    /// <summary>Position in listings. Lower comes first.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Whether the account is archived. Archived accounts keep their history but are not
    /// offered when registering.
    /// </summary>
    public bool IsArchived { get; private set; }

    /// <summary>When the account was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Whether the account holds real money, as opposed to being a nominal income or expense
    /// account.
    /// </summary>
    /// <remarks>
    /// Every "how much money is there" query filters on this. Miss it and the accumulated
    /// expense total gets added to the bank balance.
    /// </remarks>
    public bool IsReal => Type is AccountType.Checking
        or AccountType.Savings
        or AccountType.Cash
        or AccountType.CreditCard;

    /// <summary>
    /// Creates an account.
    /// </summary>
    /// <param name="householdId">Household that owns it.</param>
    /// <param name="name">Visible name. Required.</param>
    /// <param name="type">What kind of account it is.</param>
    /// <param name="currency">Currency of the account.</param>
    /// <param name="isTracked">
    /// Whether it takes part in projection. Real accounts default to <see langword="true"/>;
    /// nominal ones are never tracked, whatever is passed here.
    /// </param>
    /// <param name="displayOrder">Position in listings.</param>
    /// <param name="minimumBufferTarget">Optional balance floor.</param>
    /// <param name="createdAt">Creation instant. Defaults to now.</param>
    /// <returns>The new account.</returns>
    /// <example>
    /// <code>
    /// var santander = Account.Create(
    ///     householdId,
    ///     "Santander joint",
    ///     AccountType.Checking,
    ///     CurrencyCode.Euro,
    ///     minimumBufferTarget: 800m);
    /// </code>
    /// </example>
    public static Account Create(
        Guid householdId,
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

        // A nominal balance is an accumulated total, not money. Forced here rather than left to
        // the caller: the projection picking one up would give a plausible, wrong figure.
        if (!account.IsReal)
        {
            account.IsTracked = false;
        }

        account.MinimumBufferTarget = minimumBufferTarget;

        return account;
    }

    /// <summary>Renames the account.</summary>
    /// <param name="name">New name. Required.</param>
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// Archives the account: it keeps its history but stops being offered when registering.
    /// </summary>
    public void Archive() => IsArchived = true;
}

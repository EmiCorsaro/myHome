namespace MyHome.Modules.Ledger.Domain;

/// <summary>
/// What an account is, which determines how its balance is read.
/// </summary>
/// <remarks>
/// The first four hold real money. <see cref="Income"/> and <see cref="Expense"/> are the
/// bookkeeping counterparts: money has to come from somewhere and go somewhere, so these are
/// where it enters and leaves the household. Their balance is an accumulated total, not cash.
/// </remarks>
public enum AccountType
{
    /// <summary>Current account. Money available today.</summary>
    Checking = 1,

    /// <summary>Savings account. Money set aside, but still money.</summary>
    Savings = 2,

    /// <summary>Physical cash.</summary>
    Cash = 3,

    /// <summary>
    /// Credit card. A liability: its balance is negative and represents what is owed, not what
    /// is available.
    /// </summary>
    CreditCard = 4,

    /// <summary>Where income comes from. Nominal.</summary>
    Income = 5,

    /// <summary>Where expenses go to. Nominal.</summary>
    Expense = 6,
}

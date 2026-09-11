namespace MyHome.Modules.Ledger.Domain;

/// <summary>
/// Where the money an income budget line expects comes from.
/// </summary>
/// <remarks>
/// <para>
/// The origin answers a question the category cannot: how much of what comes in is work and how
/// much is capital. Reading that off the category tree would force the household to build its
/// tree around that question, and the tree is the household's business (D9, D20). So the origin
/// travels as an attribute of its own, next to the category rather than instead of it.
/// </para>
/// <para>
/// The list is closed: the household neither extends it nor renames it. Anything that fits none
/// of the eight is declared as <see cref="Other"/>.
/// </para>
/// <para>
/// This is deliberately not <see cref="IncomeSource"/>. That enum belongs to the
/// <see cref="Income"/> entity, which story 052 does not touch, and it carries a ninth value
/// (<c>Bonus</c>) that the closed list of this story does not have. Sharing it would quietly
/// widen the list the spec closes.
/// </para>
/// </remarks>
public enum BudgetIncomeOrigin
{
    /// <summary>Wages from employment. "Nómina".</summary>
    Payroll = 1,

    /// <summary>Billing from one's own trade or business. "Actividad propia".</summary>
    SelfEmployment = 2,

    /// <summary>Rent collected from a property. "Alquiler".</summary>
    Rental = 3,

    /// <summary>Returns on capital: interest, dividends, coupons. "Inversión".</summary>
    Investment = 4,

    /// <summary>A public allowance or subsidy. "Prestación".</summary>
    Benefit = 5,

    /// <summary>Money coming back: a tax refund, a returned purchase. "Devolución".</summary>
    Refund = 6,

    /// <summary>Money given, with nothing expected back. "Regalo".</summary>
    Gift = 7,

    /// <summary>Anything the other seven do not cover. "Otros".</summary>
    Other = 8,
}

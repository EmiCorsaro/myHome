namespace MyHome.Modules.Ledger.Contracts;

/// <summary>
/// Marker for the Ledger module. Used to locate this assembly by reflection without depending on
/// any concrete type.
/// </summary>
/// <remarks>
/// Ledger is the system's functional core: accounts, categories, journal entries and postings,
/// recurrences and balance projection. Still empty; filled in during sub-phase 1.1.
/// </remarks>
public static class LedgerModule
{
    /// <summary>Name of the database schema owned by the module.</summary>
    public const string Schema = "ledger";
}

namespace MyHome.Modules.Ledger.Contracts;

/// <summary>
/// Marker for the Ledger module. Used to locate this assembly by reflection without depending on
/// any concrete type.
/// </summary>
/// <remarks>
/// <para>
/// Ledger is the system's functional core: accounts, categories, journal entries and postings,
/// recurrences and balance projection.
/// </para>
/// <para>
/// <b>Rule for every service interface the module publishes:</b> none of them takes a household
/// identifier. It comes from the request's tenant context inside the implementation. Reading
/// another household's data by passing the wrong argument is not something a caller can express.
/// </para>
/// <para>
/// Those interfaces live in <c>Application/Interfaces</c>, beside the services that fulfil them,
/// while this namespace holds the data contracts they exchange. What keeps the implementation
/// private is visibility rather than namespace: every service is <c>internal</c>, so an outside
/// assembly can only reach the interfaces. <c>ModuleBoundaryTests</c> enforces it.
/// </para>
/// </remarks>
public static class LedgerModule
{
    /// <summary>Name of the database schema owned by the module.</summary>
    public const string Schema = "ledger";
}

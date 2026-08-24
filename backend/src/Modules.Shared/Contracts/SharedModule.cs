namespace MyHome.Modules.Shared.Contracts;

/// <summary>
/// Marker for the shared kernel. Used to locate this assembly by reflection without depending on
/// any concrete type.
/// </summary>
/// <remarks>
/// <para>
/// The shared kernel is not a module: it holds the vocabulary every module needs and none of them
/// owns — Money, CurrencyCode, Household, Member, tenancy — and everyone is allowed to depend on
/// all of it. That is why the architecture tests enforce a contracts boundary on modules but not
/// here.
/// </para>
/// <para>
/// This <c>Contracts</c> folder is therefore organisation, not a wall: it holds the shapes the
/// HTTP layer and other modules consume — household views, the validation error — while the
/// service interfaces live in <c>Application/Interfaces</c> beside the services that fulfil them.
/// Same layout as every module.
/// </para>
/// </remarks>
public static class SharedModule
{
    /// <summary>Name of the database schema owned by the shared kernel.</summary>
    public const string Schema = "shared";
}

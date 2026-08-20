namespace MyHome.Modules.Shared.Application;

/// <summary>
/// Queries about the household of the current request.
/// </summary>
/// <remarks>
/// This is the only door through which the HTTP layer obtains household data. The endpoint does
/// not know a database exists: it asks for the current household and returns what it is given.
/// </remarks>
public interface IHouseholdDirectory
{
    /// <summary>
    /// Returns the current request's household together with its members.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The household, or <c>null</c> if the resolved identifier no longer exists.</returns>
    /// <exception cref="InvalidOperationException">
    /// If no household has been resolved for the request.
    /// </exception>
    /// <example>
    /// <code>
    /// app.MapGet("/api/household", async (IHouseholdDirectory directory, CancellationToken ct) =>
    ///     await directory.GetCurrentAsync(ct) is { } household
    ///         ? Results.Ok(household)
    ///         : Results.NotFound());
    /// </code>
    /// </example>
    Task<HouseholdSummary?> GetCurrentAsync(CancellationToken cancellationToken = default);
}

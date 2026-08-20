namespace MyHome.Modules.Shared.Tenancy;

/// <summary>
/// Answers "which household does this request belong to?" for the whole of its lifetime.
/// </summary>
/// <remarks>
/// <para>
/// This is the piece tenant isolation hangs from. Repositories apply a global filter on
/// <see cref="HouseholdId"/>, so a query that forgets the filter cannot return someone else's
/// data.
/// </para>
/// <para>
/// It is resolved per request scope. Outside an HTTP request (background jobs, migrations) the
/// household has to be set explicitly.
/// </para>
/// </remarks>
public interface ITenantContext
{
    /// <summary>The current request's household, or <c>null</c> if none has been resolved.</summary>
    Guid? HouseholdId { get; }

    /// <summary>The member making the request, or <c>null</c> if it is unauthenticated.</summary>
    Guid? MemberId { get; }

    /// <summary>Whether a household has been resolved for this request.</summary>
    bool IsResolved => HouseholdId.HasValue;

    /// <summary>
    /// Returns the request's household, failing if there is none.
    /// </summary>
    /// <returns>The household identifier.</returns>
    /// <exception cref="InvalidOperationException">
    /// If called outside a scope with a resolved household. This is a programming error, not
    /// invalid input: it means domain code was reached without going through tenant resolution.
    /// </exception>
    Guid RequireHouseholdId() =>
        HouseholdId ?? throw new InvalidOperationException(
            "No household resolved for this request. Every domain operation needs one.");
}

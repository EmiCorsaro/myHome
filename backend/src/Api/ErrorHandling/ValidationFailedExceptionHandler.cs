using MyHome.Modules.Shared.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace MyHome.Api.ErrorHandling;

/// <summary>
/// Turns a <see cref="ValidationFailedException"/> into a 400 with the errors per field.
/// </summary>
/// <remarks>
/// One handler for the whole API, so endpoints carry no <c>try</c> and no error mapping, and every
/// validation error in the product comes out the same shape: RFC 9457's validation problem, an
/// <c>errors</c> object keyed by field name.
/// </remarks>
public sealed class ValidationFailedExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// Handles the exception if it is a validation failure; otherwise lets it through.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="exception">The exception thrown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if the response was written here, <see langword="false"/> to let
    /// the next handler deal with it.
    /// </returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationFailedException validation)
        {
            return false;
        }

        var errors = validation.Errors.ToDictionary(e => e.Key, e => e.Value);

        await Results.ValidationProblem(
                errors,
                detail: "Some fields need correcting before this can be saved.",
                title: "Invalid request")
            .ExecuteAsync(httpContext)
            .ConfigureAwait(false);

        return true;
    }
}

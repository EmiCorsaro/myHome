using MyHome.Modules.Shared.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace MyHome.Api.ErrorHandling;

public sealed class ValidationFailedExceptionHandler : IExceptionHandler
{
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

using Microsoft.AspNetCore.Diagnostics;

namespace MyHome.Api.ErrorHandling;

/// <summary>
/// Swallows the cancellation that happens when the caller hangs up mid-request.
/// </summary>
/// <remarks>
/// Minimal APIs bind <c>CancellationToken</c> to <c>HttpContext.RequestAborted</c>, and we pass it
/// all the way down to EF Core. When the browser drops the connection the token trips and Npgsql
/// throws <see cref="OperationCanceledException"/> from whatever query was in flight. Nothing went
/// wrong: the answer is no longer wanted.
/// <para>
/// It shows up constantly in development because React StrictMode mounts every component twice,
/// so TanStack Query starts each request, aborts it, and starts it again. Whether the abort lands
/// before or during the query is a race, which is why it looks intermittent.
/// </para>
/// <para>
/// Without this the exception reaches the error middleware, gets logged as a failure and has a 500
/// written to a socket that is already gone.
/// </para>
/// </remarks>
public sealed class ClientDisconnectedExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// Nginx's convention for "client closed request". Not in <c>StatusCodes</c> because it is not
    /// a real HTTP status; it only ever reaches the logs.
    /// </summary>
    private const int ClientClosedRequest = 499;

    /// <summary>
    /// Handles the exception if the caller is gone; otherwise lets it through.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="exception">The exception thrown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if this was a client disconnect, <see langword="false"/> to let the
    /// next handler deal with it.
    /// </returns>
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Both conditions matter. A plain OperationCanceledException with the connection still up
        // is a timeout or a bug, and hiding it would be a good way never to find out.
        if (exception is not OperationCanceledException
            || !httpContext.RequestAborted.IsCancellationRequested)
        {
            return ValueTask.FromResult(false);
        }

        if (!httpContext.Response.HasStarted)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
        }

        return ValueTask.FromResult(true);
    }
}

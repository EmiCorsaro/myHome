namespace MyHome.Modules.Shared.Contracts;

/// <summary>
/// Thrown when a request does not satisfy the rules of the operation it is asking for.
/// </summary>
/// <remarks>
/// Lets a module report validation without agreeing with the HTTP layer on a validation library.
/// Services throw this; the API turns it into a 400, a background job would just log it.
/// <para>
/// Keyed by field name, matching RFC 9457's validation problem, so the frontend can put each
/// message next to its own input instead of dumping them above the form.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// throw new ValidationFailedException(new Dictionary&lt;string, string[]&gt;
/// {
///     ["amount"] = ["The amount must be greater than zero."],
/// });
/// </code>
/// </example>
public sealed class ValidationFailedException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors =
        new Dictionary<string, string[]>();

    /// <summary>Creates the exception with no detail. Prefer the overload taking errors.</summary>
    public ValidationFailedException()
        : this(NoErrors)
    {
    }

    /// <summary>Creates the exception with a message and no per-field errors.</summary>
    /// <param name="message">Message describing the failure.</param>
    public ValidationFailedException(string message)
        : base(message) => Errors = NoErrors;

    /// <summary>Creates the exception wrapping another one.</summary>
    /// <param name="message">Message describing the failure.</param>
    /// <param name="innerException">Underlying exception.</param>
    public ValidationFailedException(string message, Exception innerException)
        : base(message, innerException) => Errors = NoErrors;

    /// <summary>Creates the exception with the errors per field.</summary>
    /// <param name="errors">Messages keyed by field name.</param>
    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("The request is not valid.") =>
        Errors = errors ?? NoErrors;

    /// <summary>Messages keyed by field name. Empty if the failure is not tied to a field.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

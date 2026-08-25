namespace MyHome.Modules.Shared.Contracts;

public sealed class ValidationFailedException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors =
        new Dictionary<string, string[]>();

    public ValidationFailedException()
        : this(NoErrors)
    {
    }

    public ValidationFailedException(string message)
        : base(message) => Errors = NoErrors;

    public ValidationFailedException(string message, Exception innerException)
        : base(message, innerException) => Errors = NoErrors;

    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("The request is not valid.") =>
        Errors = errors ?? NoErrors;

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

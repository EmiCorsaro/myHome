using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Application;

internal sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    /// <summary>Longest name an account may have, once the outer spaces are gone.</summary>
    internal const int MaxNameLength = 80;

    /// <summary>The only types a user may declare (RF-2).</summary>
    /// <remarks>
    /// The types the system keeps to classify income and expense are deliberately absent: they are
    /// created and maintained by the ledger itself, never asked for from outside (RF-9).
    /// </remarks>
    internal static readonly string[] DeclarableTypes =
        ["checking", "savings", "cash", "creditCard"];

    public CreateAccountRequestValidator()
    {
        // Both rules look at the trimmed name, because that is what gets stored: "  Nómina  " is
        // the same name as "Nómina", and a name of exactly 80 characters typed with a trailing
        // space is still 80 characters long (RF-3, RF-4 and the edge cases about outer spaces).
        RuleFor(r => r.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Escribe un nombre para la cuenta.")
            .Must(name => name.Trim().Length <= MaxNameLength)
            .WithMessage("El nombre no puede superar los 80 caracteres.");

        RuleFor(r => r.Type)
            .Must(type => DeclarableTypes.Contains(type, StringComparer.Ordinal))
            .WithMessage(
                "Elige un tipo de cuenta válido (checking, savings, cash o creditCard).");
    }
}

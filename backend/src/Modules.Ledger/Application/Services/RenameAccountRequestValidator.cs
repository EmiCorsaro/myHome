using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Application;

internal sealed class RenameAccountRequestValidator : AbstractValidator<RenameAccountRequest>
{
    public RenameAccountRequestValidator()
    {
        // The same rules story 002 fixed for a name at creation: it competes for the very same
        // namespace, so it cannot be held to a looser standard once the account already exists.
        RuleFor(r => r.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Escribe un nombre para la cuenta.")
            .Must(name => name.Trim().Length <= CreateAccountRequestValidator.MaxNameLength)
            .WithMessage("El nombre no puede superar los 80 caracteres.");
    }
}

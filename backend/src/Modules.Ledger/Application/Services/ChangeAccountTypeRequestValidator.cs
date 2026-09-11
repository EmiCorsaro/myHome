using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Application;

internal sealed class ChangeAccountTypeRequestValidator : AbstractValidator<ChangeAccountTypeRequest>
{
    public ChangeAccountTypeRequestValidator()
    {
        // The same four declarable types story 002 fixed at creation. Whether the credit-card
        // transition this type names is actually allowed is decided by the registrar (RF-11), not
        // by this shape check.
        RuleFor(r => r.Type)
            .Must(type => CreateAccountRequestValidator.DeclarableTypes
                .Contains(type, StringComparer.Ordinal))
            .WithMessage("Elige un tipo de cuenta válido (checking, savings, cash o creditCard).");
    }
}

using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Categories;

namespace MyHome.Modules.Ledger.Application;

internal sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    /// <summary>Longest name a category may have, once the outer spaces are gone.</summary>
    internal const int MaxNameLength = 80;

    public CreateCategoryRequestValidator()
    {
        // Both rules look at the trimmed name, because that is what gets stored: "  Ocio  " is
        // the same name as "Ocio", and a name of exactly 80 characters typed with a trailing
        // space is still 80 characters long (RF-6 and RF-12).
        RuleFor(r => r.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Escribe un nombre para la categoría.")
            .Must(name => name.Trim().Length <= MaxNameLength)
            .WithMessage("El nombre no puede superar los 80 caracteres.");

        RuleFor(r => r.Kind)
            .Must(kind => kind is "income" or "expense")
            .WithMessage("Elige un tipo de categoría válido (income o expense).");
    }
}

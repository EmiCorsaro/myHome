using System.Globalization;
using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Dashboard;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Which months the dashboard can be asked for (story 062).
/// </summary>
internal sealed class DashboardMonthRequestValidator : AbstractValidator<DashboardMonthRequest>
{
    /// <summary>The first year the dashboard accepts (RF-11).</summary>
    public const int FirstYear = 2015;

    /// <summary>The last year the dashboard accepts (RF-11). Provisional, per the spec's notes.</summary>
    public const int LastYear = 2100;

    private const int YearDigits = 4;

    // Longer than any month worth parsing, short enough that int parsing can never overflow.
    private const int MaxMonthDigits = 9;

    public DashboardMonthRequestValidator()
    {
        // RF-8, RF-9: both or neither. Each missing half is reported under its own name (RF-15).
        RuleFor(r => r.Year)
            .NotNull()
            .When(r => r.Month is not null)
            .WithMessage("Indica también el año del mes que quieres consultar.");

        RuleFor(r => r.Month)
            .NotNull()
            .When(r => r.Year is not null)
            .WithMessage("Indica también el mes del año que quieres consultar.");

        // RF-4, RF-12: a whole number written with four digits; RF-11: inside the accepted range.
        RuleFor(r => r.Year)
            .Cascade(CascadeMode.Stop)
            .Must(year => ParseDigits(year, YearDigits, YearDigits) is not null)
            .WithMessage("El año debe ser un número entero de cuatro cifras.")
            .Must(year => ParseDigits(year, YearDigits, YearDigits) is >= FirstYear and <= LastYear)
            .WithMessage($"El año debe estar entre {FirstYear} y {LastYear}.")
            .When(r => r.Year is not null);

        // RF-5, RF-12, RF-13: a whole number; RF-6: a leading zero is the same month; RF-10: 1-12.
        RuleFor(r => r.Month)
            .Cascade(CascadeMode.Stop)
            .Must(month => ParseDigits(month, 1, MaxMonthDigits) is not null)
            .WithMessage("El mes debe ser un número entero del 1 al 12.")
            .Must(month => ParseDigits(month, 1, MaxMonthDigits) is >= 1 and <= 12)
            .WithMessage("El mes debe estar entre 1 y 12.")
            .When(r => r.Month is not null);
    }

    /// <summary>
    /// The month a request that passed validation asks for, or <see langword="null"/> for the
    /// household's current month.
    /// </summary>
    public static DateOnly? FirstDayOf(DashboardMonthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request is { Year: not null, Month: not null }
            ? new DateOnly(
                ParseDigits(request.Year, YearDigits, YearDigits)!.Value,
                ParseDigits(request.Month, 1, MaxMonthDigits)!.Value,
                1)
            : null;
    }

    /// <summary>
    /// Reads a value made only of ASCII digits: no sign, no spaces, no decimals, no separators.
    /// </summary>
    private static int? ParseDigits(string? value, int minLength, int maxLength) =>
        value is not null
            && value.Length >= minLength
            && value.Length <= maxLength
            && value.All(char.IsAsciiDigit)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
}

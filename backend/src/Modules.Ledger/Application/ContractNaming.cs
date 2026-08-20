using System.Globalization;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Turns domain enums into the strings the contracts publish.
/// </summary>
/// <remarks>
/// Enums cross the API as text. "creditCard" is readable in a log and in the network tab; 4 is
/// not, and quietly changes meaning if someone inserts a value in the middle of the enum.
/// <para>
/// Kept out of the enums themselves so renaming a C# member stays a decision about the code
/// rather than an accidental break of the published contract.
/// </para>
/// </remarks>
internal static class ContractNaming
{
    /// <summary>
    /// Converts an enum member to its camelCase name.
    /// </summary>
    /// <param name="value">Enum member.</param>
    /// <returns>The name with a lower-case first letter, for instance <c>creditCard</c>.</returns>
    public static string ToContractName(this Enum value)
    {
        var name = value.ToString();

        return string.Create(
            name.Length,
            name,
            (destination, source) =>
            {
                source.AsSpan().CopyTo(destination);
                destination[0] = char.ToLower(destination[0], CultureInfo.InvariantCulture);
            });
    }
}

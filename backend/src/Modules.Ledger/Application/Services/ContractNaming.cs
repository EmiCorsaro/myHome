using System.Globalization;

namespace MyHome.Modules.Ledger.Application;

internal static class ContractNaming
{
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

using System.Globalization;
using System.Text;
using MyHome.Modules.Ledger.Domain;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Alphabetical order of categories by name, ignoring case and accents (story 007, RF-6, RF-9,
/// RF-10).
/// </summary>
/// <remarks>
/// <para>
/// The usual tools are not available: the build runs with <c>InvariantGlobalization</c>, where
/// culture-aware comparison is ordinal and <see cref="string.Normalize()"/> returns its input
/// untouched. So the folding is done here: upper-case the name, drop combining marks, and replace
/// each precomposed Latin letter by its base letter.
/// </para>
/// <para>
/// The table covers the precomposed upper-case letters of Latin-1 Supplement, Latin Extended-A
/// and Latin Extended-B whose canonical decomposition is an ASCII letter followed only by
/// combining marks. It was generated from the Unicode decomposition data, not typed by hand.
/// Letters with no such decomposition (Æ, Ø, ß, Đ, Ł...) are kept as they are and sort after
/// the ASCII letters.
/// </para>
/// <para>
/// Names that fold to the same key ("Agil" and "ágil") fall back to ordinal order, so the result
/// never depends on the order rows came from the database.
/// </para>
/// </remarks>
internal sealed class CategoryNameOrder : IComparer<Category>
{
    public static readonly CategoryNameOrder Instance = new();

    internal const string Accented =
        "ÀÁÂÃÄÅÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜÝĀĂĄĆĈĊČĎĒĔĖĘĚĜĞĠĢĤĨĪĬĮİĴĶĹĻĽŃŅŇŌŎŐŔŖŘŚŜŞŠŢŤŨŪŬŮŰŲŴŶŸŹŻŽ"
        + "ƠƯǍǏǑǓǕǗǙǛǞǠǦǨǪǬǰǴǸǺȀȂȄȆȈȊȌȎȐȒȔȖȘȚȞȦȨȪȬȮȰȲ";

    internal const string Unaccented =
        "AAAAAACEEEEIIIINOOOOOUUUUYAAACCCCDEEEEEGGGGHIIIIIJKLLLNNNOOORRRSSSSTTUUUUUUWYYZZZ"
        + "OUAIOUUUUUAAGKOOJGNAAAEEIIOORRUUSTHAEOOOOY";

    private CategoryNameOrder()
    {
    }

    public int Compare(Category? x, Category? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var byKey = string.CompareOrdinal(SortKey(x.Name), SortKey(y.Name));

        if (byKey != 0)
        {
            return byKey;
        }

        var byName = string.CompareOrdinal(x.Name, y.Name);

        return byName != 0 ? byName : x.PublicId.CompareTo(y.PublicId);
    }

    /// <summary>
    /// The name as it is compared: upper case, without accents.
    /// </summary>
    /// <param name="name">A category name.</param>
    internal static string SortKey(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var key = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            var upper = char.ToUpperInvariant(character);

            if (CharUnicodeInfo.GetUnicodeCategory(upper) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var index = Accented.IndexOf(upper, StringComparison.Ordinal);

            key.Append(index >= 0 ? Unaccented[index] : upper);
        }

        return key.ToString();
    }
}

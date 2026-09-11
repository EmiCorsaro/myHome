using System.Linq.Expressions;
using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

internal sealed class CategoryRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<CreateCategoryRequest> validator) : ICategoryRegistrar
{
    public async Task<CategorySummary> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await validator.ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!validation.IsValid)
        {
            throw new ValidationFailedException(
                validation.Errors
                    .GroupBy(e => ToFieldName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var householdId = tenant.RequireHouseholdId();
        var name = request.Name.Trim();
        var kind = ToDomainKind(request.Kind);

        Category? parent = null;

        if (request.ParentId is { } parentPublicId)
        {
            parent = await db.Categories
                .FirstOrDefaultAsync(
                    c => c.PublicId == parentPublicId && c.HouseholdId == householdId && !c.IsArchived,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw Invalid("parentId", "Esa categoría padre no está disponible.");

            if (parent.ParentId is not null)
            {
                throw Invalid("parentId", "Solo se admite un nivel de subcategorías.");
            }

            if (parent.Kind != kind)
            {
                throw Invalid(
                    "parentId",
                    "La subcategoría debe ser del mismo tipo que su categoría principal.");
            }
        }

        // The name is taken regardless of the kind of the existing category and regardless of
        // whether it is archived: both are deliberate (see RF-5 and the last edge case of the
        // spec), and CategoryRegistrarTests pins them down.
        var alreadyExists = await db.Categories
            .AnyAsync(NameAlreadyTaken(householdId, name), cancellationToken)
            .ConfigureAwait(false);

        if (alreadyExists)
        {
            throw Invalid("name", DuplicateNameMessage);
        }

        var category = Category.Create(
            householdId, name, kind, ColorIndexFor(name), parentId: parent?.Id);

        db.Categories.Add(category);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // The row was refused, so it is not there: stop tracking it before asking anything
            // else of this context.
            db.Entry(category).State = EntityState.Detached;

            // Lost the race: another member of the household saved the same name between the
            // check above and this insert, and the unique index refused the row. The caller gets
            // the same error it would have got a millisecond earlier, so losing the race is not
            // something a client has to know about. Any other failed insert is not ours to
            // explain and travels on.
            if (await NameWasTakenMeanwhileAsync(householdId, name, cancellationToken)
                .ConfigureAwait(false))
            {
                throw Invalid("name", DuplicateNameMessage);
            }

            throw;
        }

        return CategoryDirectory.ToSummary(category, parent?.PublicId);
    }

    /// <summary>
    /// Tells apart the lost race from any other failed insert.
    /// </summary>
    /// <param name="householdId">Household the category would have belonged to.</param>
    /// <param name="name">Name asked for, already trimmed.</param>
    /// <returns><c>true</c> when the name is taken now, after the insert was refused.</returns>
    /// <remarks>
    /// Asking the database again, rather than reading the provider's error code, keeps this free
    /// of PostgreSQL's SQLSTATE and lets the same code path be exercised by a test running on a
    /// different provider. It also answers the question that actually matters — is the name taken?
    /// — instead of the question the driver answers, which is which constraint complained.
    /// </remarks>
    private async Task<bool> NameWasTakenMeanwhileAsync(
        int householdId,
        string name,
        CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .AnyAsync(NameAlreadyTaken(householdId, name), cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The predicate that decides whether a household already uses a name.
    /// </summary>
    /// <param name="householdId">Household the category would belong to.</param>
    /// <param name="name">Name asked for, already trimmed.</param>
    /// <returns>A predicate the database provider can translate on its own.</returns>
    /// <remarks>
    /// Case-insensitivity is expressed as <c>lower(name) = lower(@name)</c> instead of
    /// <see cref="string.Equals(string, string, StringComparison)"/>, which no provider can
    /// translate and which therefore only fails once a real database is on the other end.
    /// It is a separate member so a test can assert it still translates to SQL.
    /// </remarks>
    internal static Expression<Func<Category, bool>> NameAlreadyTaken(int householdId, string name)
    {
        var comparable = Comparable(name);

#pragma warning disable CA1304, CA1311, CA1862 // Translated by the provider; no .NET culture is involved.
        return c => c.HouseholdId == householdId && c.Name.ToLower() == comparable;
#pragma warning restore CA1304, CA1311, CA1862
    }

    /// <summary>
    /// Picks the tone of the palette a category is shown with.
    /// </summary>
    /// <param name="name">Name of the category, already trimmed.</param>
    /// <returns>A tone between 1 and <see cref="Category.PaletteSize"/>.</returns>
    /// <remarks>
    /// Derived from the name, so it is stable: the same category always gets the same colour, no
    /// matter how many categories the household has, how many of them are archived, or in which
    /// order they were created. The previous "count them all and add one" grew without bound and
    /// made the colour depend on rows the user cannot see.
    /// </remarks>
    internal static int ColorIndexFor(string name)
    {
        // FNV-1a over the normalised name. Any stable function would do; String.GetHashCode is
        // randomised per process and would give the same category a different colour per run.
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var character in Comparable(name))
        {
            hash = (hash ^ character) * prime;
        }

        return (int)(hash % Category.PaletteSize) + 1;
    }

    private const string DuplicateNameMessage = "Ya existe una categoría con ese nombre.";

    private static string Comparable(string name) => name.Trim().ToLowerInvariant();

    private static CategoryKind ToDomainKind(string kind) => kind switch
    {
        "income" => CategoryKind.Income,
        "expense" => CategoryKind.Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown category kind."),
    };

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}

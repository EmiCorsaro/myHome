using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Ledger.Persistence;

/// <summary>
/// Gives a household its starting accounts and categories.
/// </summary>
/// <remarks>
/// Development only, and data only: the schema is <see cref="LedgerSchema"/>'s job and has already
/// been applied by the time this runs.
/// <para>
/// <b>This is really product logic wearing a seeder's clothes.</b> A real household created
/// tomorrow will want the same starting chart of accounts, so this belongs in whatever handles
/// household creation, not here. It stays until that exists.
/// </para>
/// </remarks>
public static class LedgerSeeder
{
    /// <summary>
    /// Accounts the household starts with. These names are shown to the user, so they are in
    /// Spanish; edit them here until there is a screen for it.
    /// </summary>
    private static readonly (string Name, AccountType Type, bool Tracked, decimal? Buffer)[]
        StarterAccounts =
        [
            ("Santander conjunta", AccountType.Checking, true, 800m),
            ("Tarjeta Santander", AccountType.CreditCard, false, null),
            ("Efectivo", AccountType.Cash, false, null),
        ];

    /// <summary>
    /// Starting expense categories, each parent followed by its children.
    /// </summary>
    /// <remarks>
    /// Children inherit the parent's colour, so the by-category report reads as ten blocks no
    /// matter how many leaves hang underneath.
    /// </remarks>
    private static readonly (string Parent, int Color, string[] Children)[] ExpenseCategories =
        [
            ("Vivienda", 1, ["Alquiler", "Subministros comunidad"]),
            ("Supermercado", 2, []),
            ("Servicios", 3, ["Luz", "Agua", "Internet & Móvil"]),
            ("Seguros", 4, ["Seguro de salud", "Seguro de Jager"]),
            ("Suscripciones", 5, []),
            ("Ocio", 6, ["Tapeo", "Cine", "Restaurante", "Helados"]),
            ("Cuidado personal", 7, ["Gimnasio", "Peluquería", "Uñas"]),
            ("Jager", 8, ["Comida", "Peluquería canina", "Veterinario"]),
            ("Formación", 9, ["UAX"]),
            ("Irregulares", 10, ["Ropa", "Regalos", "Tecnología", "Viajes", "Salud"]),
        ];

    /// <summary>Starting income categories.</summary>
    private static readonly (string Name, int Color)[] IncomeCategories =
        [
            ("Nómina", 2),
            ("Facturación", 7),
            ("Pagas Extra", 6),
            ("Devoluciones/Reembolsos", 6),
            ("Otros ingresos", 6),
        ];

    /// <summary>
    /// Ensures the household has its starting accounts and categories.
    /// </summary>
    /// <param name="services">The application's service provider.</param>
    /// <param name="householdId">Household to seed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the module is ready.</returns>
    /// <example>
    /// <code>
    /// await LedgerSeeder.EnsureSeededAsync(app.Services, householdId);
    /// </code>
    /// </example>
    public static async Task EnsureSeededAsync(
        IServiceProvider services,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        await EnsureDataAsync(db, householdId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureDataAsync(
        LedgerDbContext db,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var hasAccounts = await db.Accounts
            .AnyAsync(a => a.HouseholdId == householdId, cancellationToken)
            .ConfigureAwait(false);

        if (hasAccounts)
        {
            return;
        }

        var order = 0;

        foreach (var (name, type, tracked, buffer) in StarterAccounts)
        {
            db.Accounts.Add(Account.Create(
                householdId,
                name,
                type,
                CurrencyCode.Euro,
                isTracked: tracked,
                displayOrder: order += 10,
                minimumBufferTarget: buffer));
        }

        // The nominal pair. Nobody sees these; they are the other side of every entry, and what
        // lets income and expense be totals instead of special cases.
        db.Accounts.Add(Account.Create(
            householdId, "Ingresos", AccountType.Income, CurrencyCode.Euro, displayOrder: 900));
        db.Accounts.Add(Account.Create(
            householdId, "Gastos", AccountType.Expense, CurrencyCode.Euro, displayOrder: 910));

        order = 0;

        foreach (var (parentName, color, children) in ExpenseCategories)
        {
            var parent = Category.Create(
                householdId,
                parentName,
                CategoryKind.Expense,
                colorIndex: color,
                displayOrder: order += 100);

            db.Categories.Add(parent);

            var childOrder = parent.DisplayOrder;

            foreach (var childName in children)
            {
                db.Categories.Add(Category.Create(
                    householdId,
                    childName,
                    CategoryKind.Expense,
                    colorIndex: color,
                    displayOrder: childOrder += 1,
                    parentId: parent.Id));
            }
        }

        order = 0;

        foreach (var (name, color) in IncomeCategories)
        {
            db.Categories.Add(Category.Create(
                householdId,
                name,
                CategoryKind.Income,
                colorIndex: color,
                displayOrder: order += 10));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

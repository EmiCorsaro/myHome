using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Ledger.Persistence;

/// <summary>
/// Creates the module's tables and the household's starting accounts and categories.
/// </summary>
/// <remarks>
/// Development only. Goes away when there is data worth keeping and the schema moves to versioned
/// migrations.
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
            ("N26 conjunta", AccountType.Checking, true, 150m),
            ("Santander autónomos", AccountType.Checking, false, null),
            ("N26 Emi", AccountType.Checking, false, null),
            ("N26 Agustina", AccountType.Checking, false, null),
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
            ("Vivienda", 1, ["Alquiler", "Servicios comunes"]),
            ("Supermercado", 2, []),
            ("Suministros", 3, ["Luz", "Agua", "Internet y móvil"]),
            ("Seguros", 4, ["Seguro de salud", "Seguro de Jager"]),
            ("Suscripciones", 5, []),
            ("Ocio", 6, ["Tapeo y cañas", "Cine", "Restaurante"]),
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
            ("Alquiler Barracas", 9),
            ("Otros ingresos", 6),
        ];

    /// <summary>Tables the module expects to find in its schema.</summary>
    private static readonly string[] ExpectedTables =
        ["accounts", "categories", "journal_entries", "postings", "recurring_rules"];

    /// <summary>
    /// Ensures the ledger schema exists and the household has accounts and categories.
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

        await EnsureTablesAsync(db, cancellationToken).ConfigureAwait(false);
        await EnsureDataAsync(db, householdId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates the schema if it is missing or out of date.</summary>
    /// <param name="db">Ledger data context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the tables are in place.</returns>
    /// <remarks>
    /// EnsureCreated is no use here: it asks whether the database has any tables at all, and the
    /// shared module has already created its own by the time this runs, so it decides there is
    /// nothing to do and leaves this schema empty. Whoever adds the third module will hit the
    /// same thing.
    /// <para>
    /// A missing table means the model has moved on, so the schema is dropped and rebuilt.
    /// Development data is disposable; this stops being acceptable the day migrations arrive.
    /// </para>
    /// </remarks>
    private static async Task EnsureTablesAsync(
        LedgerDbContext db,
        CancellationToken cancellationToken)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();

        if (!await creator.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await creator.CreateAsync(cancellationToken).ConfigureAwait(false);
        }

        var present = await db.Database
            .SqlQuery<string>(
                $"""
                SELECT table_name AS "Value" FROM information_schema.tables
                WHERE table_schema = {LedgerDbContext.Schema}
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ExpectedTables.All(present.Contains))
        {
            return;
        }

        if (present.Count > 0)
        {
            await db.Database
                .ExecuteSqlRawAsync(
                    $"DROP SCHEMA IF EXISTS {LedgerDbContext.Schema} CASCADE",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
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

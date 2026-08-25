using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Ledger.Persistence;

public static class LedgerSeeder
{
    private const string MainAccount = "Santander conjunta";

    private static readonly (string Name, AccountType Type, bool Tracked, decimal? Buffer)[]
        StarterAccounts =
        [
            (MainAccount, AccountType.Checking, true, 800m),
            ("Tarjeta Santander", AccountType.CreditCard, false, null),
            ("Efectivo", AccountType.Cash, false, null),
        ];

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

    private static readonly (string Name, int Color)[] IncomeCategories =
        [
            ("Nómina", 2),
            ("Facturación", 7),
            ("Pagas Extra", 6),
            ("Devoluciones/Reembolsos", 6),
            ("Otros ingresos", 6),
        ];

    private static readonly (string Description, string Category, decimal Amount,
        PlannedAmountMode Mode, int Day)[] StarterExpenseRules =
        [
            ("Telefonía móvil", "Internet & Móvil", 25m, PlannedAmountMode.Fixed, 3),
            ("Luz", "Luz", 70m, PlannedAmountMode.Estimated, 12),
        ];

    private static readonly (string Name, IncomeSource Source, string Category, decimal Amount,
        int Day)[] StarterIncomes =
        [
            ("Nómina", IncomeSource.Salary, "Nómina", 2530m, 25),
        ];

    private static readonly (string Category, decimal Amount)[] StarterBudgets =
        [
            ("Supermercado", 300m),
        ];

    public static async Task EnsureSeededAsync(
        IServiceProvider services,
        int householdId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        await EnsureDataAsync(db, householdId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureDataAsync(
        LedgerDbContext db,
        int householdId,
        CancellationToken cancellationToken)
    {
        var hasAccounts = await db.Accounts
            .AnyAsync(a => a.HouseholdId == householdId, cancellationToken)
            .ConfigureAwait(false);

        if (hasAccounts)
        {
            return;
        }

        var accounts = new Dictionary<string, Account>(StringComparer.Ordinal);
        var order = 0;

        foreach (var (name, type, tracked, buffer) in StarterAccounts)
        {
            var account = Account.Create(
                householdId,
                name,
                type,
                CurrencyCode.Euro,
                isTracked: tracked,
                displayOrder: order += 10,
                minimumBufferTarget: buffer);

            db.Accounts.Add(account);
            accounts[name] = account;
        }

        db.Accounts.Add(Account.Create(
            householdId, "Ingresos", AccountType.Income, CurrencyCode.Euro, displayOrder: 900));
        db.Accounts.Add(Account.Create(
            householdId, "Gastos", AccountType.Expense, CurrencyCode.Euro, displayOrder: 910));

        var categories = new Dictionary<string, Category>(StringComparer.Ordinal);

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
            categories[parentName] = parent;

            var childOrder = parent.DisplayOrder;

            foreach (var childName in children)
            {
                var child = Category.Create(
                    householdId,
                    childName,
                    CategoryKind.Expense,
                    colorIndex: color,
                    displayOrder: childOrder += 1,
                    parentId: parent.Id);

                db.Categories.Add(child);
                categories[childName] = child;
            }
        }

        order = 0;

        foreach (var (name, color) in IncomeCategories)
        {
            var category = Category.Create(
                householdId,
                name,
                CategoryKind.Income,
                colorIndex: color,
                displayOrder: order += 10);

            db.Categories.Add(category);
            categories[name] = category;
        }

        SeedPlanning(db, householdId, accounts[MainAccount], categories);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void SeedPlanning(
        LedgerDbContext db,
        int householdId,
        Account account,
        Dictionary<string, Category> categories)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        foreach (var (description, categoryName, amount, mode, day) in StarterExpenseRules)
        {
            db.RecurringRules.Add(RecurringRule.Create(
                householdId,
                EntryKind.Expense,
                RecurrenceFrequency.Monthly,
                account,
                categories[categoryName],
                description,
                Money.Of(amount, CurrencyCode.Euro),
                thisMonth.AddDays(day - 1),
                amountMode: mode));
        }

        foreach (var (name, source, categoryName, amount, day) in StarterIncomes)
        {
            db.Incomes.Add(Income.Create(
                householdId,
                name,
                source,
                IncomePeriodicity.Monthly,
                account,
                categories[categoryName],
                Money.Of(amount, CurrencyCode.Euro),
                thisMonth.AddDays(day - 1)));
        }

        foreach (var (categoryName, amount) in StarterBudgets)
        {
            db.CategoryBudgets.Add(CategoryBudget.Create(
                householdId,
                categories[categoryName],
                thisMonth,
                Money.Of(amount, CurrencyCode.Euro)));
        }
    }
}

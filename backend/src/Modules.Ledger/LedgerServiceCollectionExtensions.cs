using FluentValidation;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Ledger;

/// <summary>
/// Registers the Ledger module in the dependency injection container.
/// </summary>
/// <remarks>
/// This is the module's only entry point from the composition root. Everything else in the
/// module is internal: callers talk to <c>MyHome.Modules.Ledger.Contracts</c>.
/// </remarks>
public static class LedgerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Ledger module's persistence and application services.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="connectionStringName">
    /// Name of the PostgreSQL connection string. Aspire injects it under this name.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddLedgerModule();
    /// </code>
    /// </example>
    public static IServiceCollection AddLedgerModule(
        this IServiceCollection services,
        string connectionStringName = "myhomedb")
    {
        ArgumentNullException.ThrowIfNull(services);

        // Same physical database as the shared kernel, a different schema and a different
        // context. Sharing the connection keeps deployment simple today; owning the schema keeps
        // the option of moving out tomorrow.
        services.AddDbContext<LedgerDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();

            options.UseNpgsql(
                configuration.GetConnectionString(connectionStringName),
                npgsql => npgsql.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    LedgerDbContext.Schema));
        });

        services.AddScoped<IAccountDirectory, AccountDirectory>();
        services.AddScoped<ICategoryDirectory, CategoryDirectory>();
        services.AddScoped<IExpenseRegistrar, ExpenseRegistrar>();
        services.AddScoped<IDashboardQuery, DashboardQuery>();

        services.AddScoped<IValidator<RegisterExpenseRequest>, RegisterExpenseRequestValidator>();

        return services;
    }
}

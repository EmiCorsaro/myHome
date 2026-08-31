using FluentValidation;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Application.Interfaces.IncomeRegister;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Contracts.Income;
using MyHome.Modules.Ledger.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Ledger;

public static class LedgerServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerModule(
        this IServiceCollection services,
        string connectionStringName = "myhomedb")
    {
        ArgumentNullException.ThrowIfNull(services);

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
        services.AddScoped<IIncomeRegister, IncomeRegister>();
        services.AddScoped<IDashboardQuery, DashboardQuery>();

        services.AddScoped<IValidator<RegisterExpenseRequest>, RegisterExpenseRequestValidator>();
        services.AddScoped<IValidator<RegisterIncomeRequest>, RegisterIncomeRequestValidator>();

        return services;
    }
}

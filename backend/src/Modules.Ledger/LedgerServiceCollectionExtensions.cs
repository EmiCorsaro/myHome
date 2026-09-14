using FluentValidation;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Accounts;
using MyHome.Modules.Ledger.Contracts.Budget;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Contracts.Dashboard;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Contracts.Incomes;
using MyHome.Modules.Ledger.Contracts.Transfers;
using MyHome.Modules.Ledger.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddScoped<IAccountRegistrar, AccountRegistrar>();
        services.AddScoped<IOpeningBalanceRegistrar, OpeningBalanceRegistrar>();
        services.AddScoped<IAccountControlRegistrar, AccountControlRegistrar>();
        services.AddScoped<IAccountLifecycleRegistrar, AccountLifecycleRegistrar>();
        services.AddScoped<ICategoryDirectory, CategoryDirectory>();
        services.AddScoped<ICategoryRegistrar, CategoryRegistrar>();
        services.AddScoped<IExpenseRegistrar, ExpenseRegistrar>();
        services.AddScoped<IIncomeRegistrar, IncomeRegistrar>();
        services.AddScoped<ITransferRegistrar, TransferRegistrar>();
        services.AddScoped<IMovementLifecycleRegistrar, MovementLifecycleRegistrar>();
        services.AddScoped<IDashboardQuery, DashboardQuery>();
        services.AddScoped<IBudgetDirectory, BudgetDirectory>();
        services.AddScoped<IBudgetLineRegistrar, BudgetLineRegistrar>();

        // "Not in the future" is decided against the household's clock, and a test needs to be able
        // to stand on a known day. TryAdd so a host that already registered its own keeps it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IValidator<CreateAccountRequest>, CreateAccountRequestValidator>();
        services.AddScoped<IValidator<RenameAccountRequest>, RenameAccountRequestValidator>();
        services.AddScoped<IValidator<ChangeAccountTypeRequest>, ChangeAccountTypeRequestValidator>();
        services.AddScoped<IValidator<RegisterExpenseRequest>, RegisterExpenseRequestValidator>();
        services.AddScoped<IValidator<RegisterIncomeRequest>, RegisterIncomeRequestValidator>();
        services.AddScoped<IValidator<RegisterTransferRequest>, RegisterTransferRequestValidator>();
        services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
        services.AddScoped<IValidator<DeclareBudgetLineRequest>, DeclareBudgetLineRequestValidator>();
        services.AddScoped<IValidator<DashboardMonthRequest>, DashboardMonthRequestValidator>();

        return services;
    }
}

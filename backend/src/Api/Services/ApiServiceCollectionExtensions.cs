using MyHome.Api.Interfaces.Accounts;
using MyHome.Api.Interfaces.Categories;
using MyHome.Api.Interfaces.Dashboard;
using MyHome.Api.Interfaces.Expenses;
using MyHome.Api.Interfaces.Household;
using MyHome.Api.Services.Accounts;
using MyHome.Api.Services.Categories;
using MyHome.Api.Services.Dashboard;
using MyHome.Api.Services.Expenses;
using MyHome.Api.Services.Household;

namespace MyHome.Api.Services;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IHouseholdService, HouseholdService>();

        return services;
    }
}

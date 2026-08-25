using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Shared;

public static class SharedServiceCollectionExtensions
{
    public static IServiceCollection AddSharedModule(
        this IServiceCollection services,
        string connectionStringName = "myhomedb")
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<SharedDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();

            options.UseNpgsql(
                configuration.GetConnectionString(connectionStringName),
                npgsql => npgsql.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    SharedDbContext.Schema));
        });

        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());

        services.AddScoped<IHouseholdDirectory, HouseholdDirectory>();

        return services;
    }

    public static IServiceCollection AddDevelopmentHouseholdResolver(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DevelopmentHouseholdCache>();
        services.AddScoped<IHouseholdResolver, DevelopmentHouseholdResolver>();

        return services;
    }
}

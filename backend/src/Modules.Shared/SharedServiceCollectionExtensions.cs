using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Shared;

/// <summary>
/// Registers the shared module in the dependency injection container.
/// </summary>
/// <remarks>
/// Every module exposes a method like this one. The composition root (the Api project) calls
/// them and knows nothing else about each module's internals: neither its persistence types nor
/// its internal services.
/// </remarks>
public static class SharedServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence, tenant context and application services shared by every module.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="connectionStringName">
    /// Name of the PostgreSQL connection string. Aspire injects it under this name.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddSharedModule();
    /// </code>
    /// </example>
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

        // One instance per request: the tenant is resolved at the top of the pipeline and the
        // whole service graph for that request sees the same household.
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());

        services.AddScoped<IHouseholdDirectory, HouseholdDirectory>();

        return services;
    }

    /// <summary>
    /// Registers the development household resolver, which attributes any request to the first
    /// household in the database.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// <b>Call only when the environment is Development.</b> It validates no credentials. When
    /// the real identity provider arrives, this call is replaced by the registration of the
    /// resolver that reads the request's identity.
    /// </remarks>
    public static IServiceCollection AddDevelopmentHouseholdResolver(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DevelopmentHouseholdCache>();
        services.AddScoped<IHouseholdResolver, DevelopmentHouseholdResolver>();

        return services;
    }
}

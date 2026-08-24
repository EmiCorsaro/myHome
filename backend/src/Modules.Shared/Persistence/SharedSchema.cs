using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Shared.Persistence;

/// <summary>
/// Brings the shared kernel's database schema up to date.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="DevelopmentSeeder"/> on purpose: the schema is applied in every
/// environment, the sample data only in development. Folding one into the other is how a
/// production database ends up without its tables.
/// </para>
/// <para>
/// The module owns its own migration history — <c>shared.__ef_migrations_history</c> — so it
/// versions independently of every other module, and a database that is several migrations behind
/// receives exactly the ones it is missing.
/// </para>
/// </remarks>
public static class SharedSchema
{
    /// <summary>
    /// Applies any pending migrations, opening its own scope.
    /// </summary>
    /// <param name="services">The application's service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the schema matches the model.</returns>
    /// <remarks>
    /// Applying migrations from the application is fine while a single instance runs it. Two
    /// instances starting together would both attempt the same DDL. When that day comes — or when
    /// production deploys need to be approved separately from the release — this call goes away
    /// and a migration bundle takes over: <c>dotnet ef migrations bundle</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// await SharedSchema.MigrateAsync(app.Services);
    /// </code>
    /// </example>
    public static async Task MigrateAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SharedDbContext>();

        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}

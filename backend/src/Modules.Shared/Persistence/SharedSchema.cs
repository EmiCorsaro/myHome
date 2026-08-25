using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Shared.Persistence;

public static class SharedSchema
{
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

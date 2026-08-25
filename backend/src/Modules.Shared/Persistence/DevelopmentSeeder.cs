using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Shared.Persistence;

public static class DevelopmentSeeder
{
    public static async Task<int> EnsureSeededAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SharedDbContext>();

        var household = await EnsureSeededAsync(db, cancellationToken).ConfigureAwait(false);

        return household.Id;
    }

    public static async Task<Household> EnsureSeededAsync(
        SharedDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var existing = await db.Households
            .Include(h => h.Members)
            .OrderBy(h => h.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var household = Household.Create(
            name: "Development household",
            ownerDisplayName: "Member A",
            baseCurrency: CurrencyCode.Euro,
            timeZoneId: "Europe/Madrid");

        household.AddMember("Member B", MemberRole.Member);

        db.Households.Add(household);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return household;
    }
}

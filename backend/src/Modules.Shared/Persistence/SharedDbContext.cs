using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Shared.Persistence;

public sealed class SharedDbContext(DbContextOptions<SharedDbContext> options)
    : DbContext(options)
{
    public const string Schema = SharedModule.Schema;

    public const string KeySequence = "key_sequence";

    public DbSet<Household> Households => Set<Household>();

    public DbSet<HouseholdMember> Members => Set<HouseholdMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.UseHiLo(KeySequence, Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SharedDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

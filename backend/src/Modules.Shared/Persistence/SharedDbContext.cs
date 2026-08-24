using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Shared.Persistence;

/// <summary>
/// Data context for households and members.
/// </summary>
/// <remarks>
/// <para>
/// It lives in the <c>shared</c> schema. Each module will have its own context in its own
/// schema, and no module queries another's tables: they communicate through contracts and
/// events. That constraint is what keeps extracting a module to its own service cheap.
/// </para>
/// <para>
/// Modules needing household or member data ask for it here, never with a cross-schema join.
/// </para>
/// </remarks>
/// <param name="options">Context configuration options.</param>
public sealed class SharedDbContext(DbContextOptions<SharedDbContext> options)
    : DbContext(options)
{
    /// <summary>Name of the database schema this context owns.</summary>
    public const string Schema = SharedModule.Schema;

    /// <summary>Registered households.</summary>
    public DbSet<Household> Households => Set<Household>();

    /// <summary>Members across all households.</summary>
    public DbSet<HouseholdMember> Members => Set<HouseholdMember>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SharedDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

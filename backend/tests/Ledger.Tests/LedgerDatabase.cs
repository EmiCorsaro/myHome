using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Contracts.Households;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Shared.Tenancy;

namespace MyHome.Ledger.Tests;

/// <summary>
/// A ledger database a test can own end to end.
/// </summary>
/// <remarks>
/// SQLite in memory rather than the in-memory provider: it is a relational provider, so the LINQ
/// a service writes is really translated to SQL. That is the whole point here — the defect these
/// tests were written for was a predicate that compiled fine, ran fine against a fake, and threw
/// on the first real query. Production runs on PostgreSQL, so what is provider-specific gets its
/// own translation test as well (see <c>CategoryQueryTranslationTests</c>).
/// </remarks>
internal sealed class LedgerDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <param name="interceptors">
    /// Interceptors for the context, used to slip another writer in between two of its steps.
    /// </param>
    public LedgerDatabase(params IInterceptor[] interceptors)
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        Context = new LedgerDbContext(
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseSqlite(_connection)
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
                .AddInterceptors(interceptors)
                .Options);

        Context.Database.EnsureCreated();
    }

    public LedgerDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

/// <summary>
/// Trims from the model what PostgreSQL offers and SQLite does not: the hi/lo key
/// sequence and the schema the tables live in, plus the check constraints written in its dialect.
/// Nothing else about the mapping is touched, so the columns, the keys and the indexes under test
/// are the ones production uses.
/// </summary>
/// <param name="dependencies">Injected by EF Core.</param>
internal sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.Customize(modelBuilder, context);

        var model = modelBuilder.Model;

        model.SetDefaultSchema(null);

        foreach (var sequence in model.GetSequences().ToList())
        {
            model.RemoveSequence(sequence.Name, sequence.Schema);
        }

        RemoveValueGenerationStrategy(model);

        foreach (var entityType in model.GetEntityTypes())
        {
            entityType.SetSchema(null);
            RemoveValueGenerationStrategy(entityType);

            // Check constraints written in PostgreSQL's dialect (date_part, for one). They guard
            // the same rules the domain already enforces, so dropping them here costs no coverage.
            foreach (var constraint in entityType.GetDeclaredCheckConstraints().ToList())
            {
                entityType.RemoveCheckConstraint(constraint.ModelName);
            }

            foreach (var property in entityType.GetProperties())
            {
                RemoveValueGenerationStrategy(property);
                StoreInstantsAsTicks(property);

                if (property.IsPrimaryKey() && property.ClrType == typeof(int))
                {
                    property.ValueGenerated = ValueGenerated.OnAdd;
                }
            }
        }
    }

    /// <summary>
    /// Keeps instants as ticks, which SQLite can sort.
    /// </summary>
    /// <param name="property">A property of the model.</param>
    /// <remarks>
    /// SQLite refuses to order by a <see cref="DateTimeOffset"/>, and PostgreSQL orders by it every
    /// time the dashboard lists movements. Storing the same value as a number keeps the comparison
    /// and the round trip exactly as they are, and is only how these tests keep their books: nothing
    /// about the mapping under test changes.
    /// </remarks>
    private static void StoreInstantsAsTicks(IMutableProperty property)
    {
        if (property.ClrType == typeof(DateTimeOffset))
        {
            property.SetValueConverter(
                new ValueConverter<DateTimeOffset, long>(
                    value => value.UtcTicks,
                    ticks => new DateTimeOffset(ticks, TimeSpan.Zero)));
        }
        else if (property.ClrType == typeof(DateTimeOffset?))
        {
            property.SetValueConverter(
                new ValueConverter<DateTimeOffset?, long?>(
                    value => value == null ? null : value.Value.UtcTicks,
                    ticks => ticks == null ? null : new DateTimeOffset(ticks.Value, TimeSpan.Zero)));
        }
    }

    private static void RemoveValueGenerationStrategy(IMutableAnnotatable annotatable)
    {
        foreach (var annotation in annotatable.GetAnnotations().ToList())
        {
            if (annotation.Name.Contains("ValueGenerationStrategy", StringComparison.Ordinal)
                || annotation.Name.Contains("HiLoSequence", StringComparison.Ordinal))
            {
                annotatable.RemoveAnnotation(annotation.Name);
            }
        }
    }
}

/// <summary>
/// A tenant already resolved to a household, as every request has one by the time a service runs.
/// </summary>
/// <param name="householdId">Household the caller belongs to.</param>
/// <param name="memberId">Member making the request, when it matters.</param>
internal sealed class TestTenantContext(int householdId, int? memberId = null) : ITenantContext
{
    public int? HouseholdId => householdId;

    public int? MemberId => memberId;
}

/// <summary>
/// The shared module's answer about the current household, without a shared database behind it.
/// </summary>
/// <param name="currency">Currency the household keeps its books in.</param>
/// <param name="resolvableMembers">
/// The members this directory can resolve, keyed by public id. Any public id not in this map is
/// reported as belonging to a different household, which is the fixture's default: a test asks
/// for it explicitly only when it needs a member to resolve successfully.
/// </param>
internal sealed class TestHouseholdDirectory(
    CurrencyCode currency,
    IReadOnlyDictionary<Guid, int>? resolvableMembers = null) : IHouseholdDirectory
{
    public Task<HouseholdSummary?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<HouseholdSummary?>(
            new HouseholdSummary(
                Guid.CreateVersion7(),
                "Casa",
                currency.Value,
                "Europe/Madrid",
                []));

    public Task<int?> ResolveMemberAsync(
        Guid publicId, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            resolvableMembers is not null && resolvableMembers.TryGetValue(publicId, out var id)
                ? (int?)id
                : null);
}

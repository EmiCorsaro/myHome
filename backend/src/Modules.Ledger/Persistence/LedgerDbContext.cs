using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Persistence;

/// <summary>
/// Data context for the Ledger module: accounts, categories, entries and postings.
/// </summary>
/// <remarks>
/// Own schema, <c>ledger</c>, and no joins against another module's tables. Household data comes
/// from Modules.Shared through its services. That is what keeps extracting this module cheap.
/// <para>
/// Internal on purpose: nothing outside can inject it, so the ledger's rules cannot be sidestepped
/// by someone who only wanted a quick query.
/// </para>
/// </remarks>
/// <param name="options">Context configuration options.</param>
internal sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    /// <summary>Name of the database schema owned by the module.</summary>
    public const string Schema = LedgerModule.Schema;

    /// <summary>Accounts, real and nominal.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Expense and income categories.</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>Recorded entries.</summary>
    public DbSet<JournalEntry> Entries => Set<JournalEntry>();

    /// <summary>Postings, queryable on their own for balances and reports.</summary>
    public DbSet<Posting> Postings => Set<Posting>();

    /// <summary>Rules describing movements that repeat.</summary>
    public DbSet<RecurringRule> RecurringRules => Set<RecurringRule>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

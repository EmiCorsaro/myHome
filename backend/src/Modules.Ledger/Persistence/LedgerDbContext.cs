using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Persistence;

internal sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public const string Schema = LedgerModule.Schema;

    public const string KeySequence = "key_sequence";

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<JournalEntry> Entries => Set<JournalEntry>();

    public DbSet<Posting> Postings => Set<Posting>();

    public DbSet<RecurringRule> RecurringRules => Set<RecurringRule>();

    public DbSet<Income> Incomes => Set<Income>();

    public DbSet<PlannedMovement> PlannedMovements => Set<PlannedMovement>();

    public DbSet<CategoryBudget> CategoryBudgets => Set<CategoryBudget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.UseHiLo(KeySequence, Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

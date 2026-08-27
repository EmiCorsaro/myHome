using MyHome.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("journal_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id")
            .UseHiLo(LedgerDbContext.KeySequence, LedgerDbContext.Schema);

        builder.Property(e => e.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(e => e.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_journal_entries_public_id");

        builder.Property(e => e.HouseholdId).HasColumnName("household_id").IsRequired();

        builder.Property(e => e.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.OccurredOn).HasColumnName("occurred_on").IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(e => e.ClientMutationId)
            .HasColumnName("client_mutation_id")
            .HasMaxLength(64);

        builder.Property(e => e.RecurringRuleId).HasColumnName("recurring_rule_id");

        builder.HasOne(e => e.RecurringRule)
            .WithMany()
            .HasForeignKey(e => e.RecurringRuleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Postings)
            .WithOne()
            .HasForeignKey(p => p.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Postings)
            .HasField("_postings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => new { e.HouseholdId, e.OccurredOn })
            .HasDatabaseName("ix_journal_entries_household_date");

        builder.HasIndex(e => new { e.HouseholdId, e.ClientMutationId })
            .IsUnique()
            .HasFilter("client_mutation_id IS NOT NULL")
            .HasDatabaseName("ux_journal_entries_client_mutation");
    }
}

using MyHome.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

/// <summary>
/// Maps <see cref="JournalEntry"/> to the <c>ledger.journal_entries</c> table.
/// </summary>
internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("journal_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
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

        builder.HasOne<RecurringRule>()
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

        // Matches how the listing reads them, so it is a range scan and not a sort.
        builder.HasIndex(e => new { e.HouseholdId, e.OccurredOn })
            .HasDatabaseName("ix_journal_entries_household_date");

        // Makes a duplicate impossible rather than unlikely when a client retries with the same
        // key. Filtered because most entries carry no key at all.
        builder.HasIndex(e => new { e.HouseholdId, e.ClientMutationId })
            .IsUnique()
            .HasFilter("client_mutation_id IS NOT NULL")
            .HasDatabaseName("ux_journal_entries_client_mutation");
    }
}

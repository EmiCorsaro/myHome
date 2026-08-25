using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class PlannedMovementConfiguration : IEntityTypeConfiguration<PlannedMovement>
{
    public void Configure(EntityTypeBuilder<PlannedMovement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "planned_movements",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_planned_movements_single_origin",
                    "num_nonnulls(rule_id, income_id) <= 1");

                table.HasCheckConstraint(
                    "ck_planned_movements_settlement_complete",
                    "num_nonnulls(journal_entry_id, actual_amount, settled_at) IN (0, 3)");
            });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id")
            .UseHiLo(LedgerDbContext.KeySequence, LedgerDbContext.Schema);

        builder.Property(p => p.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(p => p.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_planned_movements_public_id");

        builder.Property(p => p.HouseholdId).HasColumnName("household_id").IsRequired();

        builder.Property(p => p.RuleId).HasColumnName("rule_id");
        builder.Property(p => p.IncomeId).HasColumnName("income_id");

        builder.Property(p => p.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.DueDate).HasColumnName("due_date").IsRequired();

        builder.Property(p => p.ExpectedAmount)
            .HasColumnName("expected_amount")
            .HasPrecision(19, Money.OperatingScale)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasConversion(currency => currency.Value, value => CurrencyCode.Parse(value))
            .IsRequired();

        builder.Property(p => p.AmountMode)
            .HasColumnName("amount_mode")
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(p => p.CategoryId).HasColumnName("category_id").IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.DayToleranceDays)
            .HasColumnName("day_tolerance_days")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.JournalEntryId).HasColumnName("journal_entry_id");

        builder.Property(p => p.ActualAmount)
            .HasColumnName("actual_amount")
            .HasPrecision(19, Money.OperatingScale);

        builder.Property(p => p.SettledAt).HasColumnName("settled_at");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Ignore(p => p.Variance);
        builder.Ignore(p => p.IsSettled);

        builder.HasOne<RecurringRule>().WithMany().HasForeignKey(p => p.RuleId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Income>().WithMany().HasForeignKey(p => p.IncomeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.JournalEntry)
            .WithMany()
            .HasForeignKey(p => p.JournalEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Account>().WithMany().HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.RuleId, p.DueDate })
            .IsUnique()
            .HasFilter("rule_id IS NOT NULL")
            .HasDatabaseName("ux_planned_movements_rule_due");

        builder.HasIndex(p => new { p.IncomeId, p.DueDate })
            .IsUnique()
            .HasFilter("income_id IS NOT NULL")
            .HasDatabaseName("ux_planned_movements_income_due");

        builder.HasIndex(p => p.JournalEntryId)
            .IsUnique()
            .HasFilter("journal_entry_id IS NOT NULL")
            .HasDatabaseName("ux_planned_movements_entry");

        builder.HasIndex(p => new { p.HouseholdId, p.DueDate, p.Status })
            .HasDatabaseName("ix_planned_movements_household_due");

        builder.HasIndex(p => new { p.HouseholdId, p.AccountId, p.CategoryId, p.DueDate })
            .HasDatabaseName("ix_planned_movements_match");
    }
}

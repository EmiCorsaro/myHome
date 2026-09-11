using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "budget_lines",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_budget_lines_period_start_is_first",
                    "date_part('day', period_start) = 1");

                table.HasCheckConstraint(
                    "ck_budget_lines_amount_is_positive",
                    "amount > 0");

                // RF-19 and RF-20 of story 052, as one condition because they are the two halves
                // of the same fact: the origin belongs to income and to nothing else.
                table.HasCheckConstraint(
                    "ck_budget_lines_origin_matches_sign",
                    "(sign = 'Income' AND origin IS NOT NULL) "
                        + "OR (sign = 'Expense' AND origin IS NULL)");
            });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id")
            .UseHiLo(LedgerDbContext.KeySequence, LedgerDbContext.Schema);

        builder.Property(b => b.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(b => b.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_budget_lines_public_id");

        builder.Property(b => b.HouseholdId).HasColumnName("household_id").IsRequired();

        builder.Property(b => b.Sign)
            .HasColumnName("sign")
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(b => b.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(b => b.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(b => b.PeriodStart).HasColumnName("period_start").IsRequired();

        builder.Property(b => b.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, Money.OperatingScale)
            .IsRequired();

        builder.Property(b => b.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasConversion(currency => currency.Value, value => CurrencyCode.Parse(value))
            .IsRequired();

        builder.Property(b => b.AmountMode)
            .HasColumnName("amount_mode")
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        // Nullable on purpose: an expense line has no origin at all, which is not the same as
        // having one that says "other".
        builder.Property(b => b.Origin)
            .HasColumnName("origin")
            .HasMaxLength(24)
            .HasConversion<string>();

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Ignore(b => b.PeriodEnd);
        builder.Ignore(b => b.DaysInPeriod);

        builder.HasOne<Category>().WithMany().HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>().WithMany().HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Invariant I-24: one line per household, category and month. It is the database that
        // makes it true, not the service that checks it first: two members declaring the same
        // category at the same time both pass the check and only one of them gets a row.
        builder.HasIndex(b => new { b.HouseholdId, b.CategoryId, b.PeriodStart })
            .IsUnique()
            .HasDatabaseName("ux_budget_lines_period");

        builder.HasIndex(b => new { b.HouseholdId, b.PeriodStart })
            .HasDatabaseName("ix_budget_lines_household_period");
    }
}

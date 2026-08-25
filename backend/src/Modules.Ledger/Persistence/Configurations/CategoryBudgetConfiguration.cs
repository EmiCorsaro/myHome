using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class CategoryBudgetConfiguration : IEntityTypeConfiguration<CategoryBudget>
{
    public void Configure(EntityTypeBuilder<CategoryBudget> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "category_budgets",
            table => table.HasCheckConstraint(
                "ck_category_budgets_period_start_is_first",
                "date_part('day', period_start) = 1"));

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id")
            .UseHiLo(LedgerDbContext.KeySequence, LedgerDbContext.Schema);

        builder.Property(b => b.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(b => b.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_category_budgets_public_id");

        builder.Property(b => b.HouseholdId).HasColumnName("household_id").IsRequired();

        builder.Property(b => b.CategoryId).HasColumnName("category_id").IsRequired();
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

        builder.Property(b => b.Scope)
            .HasColumnName("scope")
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Ignore(b => b.PeriodEnd);

        builder.HasOne<Category>().WithMany().HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.HouseholdId, b.CategoryId, b.PeriodStart })
            .IsUnique()
            .HasDatabaseName("ux_category_budgets_period");

        builder.HasIndex(b => new { b.HouseholdId, b.PeriodStart })
            .HasDatabaseName("ix_category_budgets_household_period");
    }
}

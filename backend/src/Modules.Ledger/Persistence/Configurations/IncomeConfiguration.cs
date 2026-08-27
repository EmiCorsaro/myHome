using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("incomes");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id")
            .UseHiLo(LedgerDbContext.KeySequence, LedgerDbContext.Schema);

        builder.Property(i => i.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(i => i.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_incomes_public_id");

        builder.Property(i => i.HouseholdId).HasColumnName("household_id").IsRequired();

        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(200).IsRequired();

        builder.Property(i => i.Source)
            .HasColumnName("source")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(i => i.Periodicity)
            .HasColumnName("periodicity")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(i => i.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(i => i.CategoryId).HasColumnName("category_id").IsRequired();

        builder.Property(i => i.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, Money.OperatingScale)
            .IsRequired();

        builder.Property(i => i.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasConversion(currency => currency.Value, value => CurrencyCode.Parse(value))
            .IsRequired();

        builder.Property(i => i.DayOfMonth).HasColumnName("day_of_month").IsRequired();
        builder.Property(i => i.DayToleranceDays)
            .HasColumnName("day_tolerance_days")
            .IsRequired();
        builder.Property(i => i.StartsOn).HasColumnName("starts_on").IsRequired();
        builder.Property(i => i.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Ignore(i => i.IsRecurring);

        builder.HasOne<Account>().WithMany().HasForeignKey(i => i.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.HouseholdId, i.IsActive })
            .HasDatabaseName("ix_incomes_household");

        builder.HasIndex(i => new { i.HouseholdId, i.StartsOn })
            .HasDatabaseName("ix_incomes_household_starts_on");
    }
}

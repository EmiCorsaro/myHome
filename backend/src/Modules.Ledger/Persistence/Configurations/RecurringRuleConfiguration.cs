using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

/// <summary>
/// Maps <see cref="RecurringRule"/> to the <c>ledger.recurring_rules</c> table.
/// </summary>
internal sealed class RecurringRuleConfiguration : IEntityTypeConfiguration<RecurringRule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RecurringRule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("recurring_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.HouseholdId).HasColumnName("household_id").IsRequired();

        builder.Property(r => r.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.Frequency)
            .HasColumnName("frequency")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(r => r.CategoryId).HasColumnName("category_id").IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, Money.OperatingScale)
            .IsRequired();

        builder.Property(r => r.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasConversion(currency => currency.Value, value => CurrencyCode.Parse(value))
            .IsRequired();

        builder.Property(r => r.DayOfMonth).HasColumnName("day_of_month").IsRequired();
        builder.Property(r => r.StartsOn).HasColumnName("starts_on").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<Account>().WithMany().HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.HouseholdId, r.IsActive })
            .HasDatabaseName("ix_recurring_rules_household");
    }
}

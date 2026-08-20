using MyHome.Modules.Shared.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Shared.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Household"/> to the <c>shared.households</c> table.
/// </summary>
public sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("households");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(h => h.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        // The currency is stored as its three letters: readable in any ad-hoc query, and with no
        // lookup table to keep in sync.
        builder.Property(h => h.BaseCurrency)
            .HasColumnName("base_currency")
            .HasMaxLength(3)
            .HasConversion(
                currency => currency.Value,
                value => CurrencyCode.Parse(value))
            .IsRequired();

        builder.Property(h => h.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasMany(h => h.Members)
            .WithOne()
            .HasForeignKey(m => m.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(h => h.Members)
            .HasField("_members")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

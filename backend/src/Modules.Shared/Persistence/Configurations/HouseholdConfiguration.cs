using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Shared.Persistence.Configurations;

public sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("households");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id")
            .UseHiLo(SharedDbContext.KeySequence, SharedDbContext.Schema);

        builder.Property(h => h.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(h => h.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_households_public_id");

        builder.Property(h => h.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

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

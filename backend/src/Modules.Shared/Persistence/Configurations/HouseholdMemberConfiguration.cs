using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Shared.Persistence.Configurations;

public sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("household_members");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id")
            .UseHiLo(SharedDbContext.KeySequence, SharedDbContext.Schema);

        builder.Property(m => m.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(m => m.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_household_members_public_id");

        builder.Property(m => m.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(m => m.UserId).HasColumnName("user_id");

        builder.Property(m => m.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at").IsRequired();

        builder.HasIndex(m => new { m.HouseholdId, m.DisplayOrder });

        builder.HasIndex(m => m.UserId)
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");
    }
}

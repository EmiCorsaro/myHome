using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Shared.Persistence.Configurations;

/// <summary>
/// Maps <see cref="HouseholdMember"/> to the <c>shared.household_members</c> table.
/// </summary>
public sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("household_members");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

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

        // A sign-in account belongs to at most one member. Without this, two members could share
        // a login and the system would not know who to attribute their entries to.
        builder.HasIndex(m => m.UserId)
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");
    }
}

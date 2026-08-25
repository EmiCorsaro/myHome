using MyHome.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id")
            .UseHiLo(LedgerDbContext.KeySequence, LedgerDbContext.Schema);

        builder.Property(c => c.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(c => c.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_categories_public_id");

        builder.Property(c => c.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(80).IsRequired();

        builder.Property(c => c.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(c => c.ParentId).HasColumnName("parent_id");
        builder.Property(c => c.ColorIndex).HasColumnName("color_index").IsRequired();
        builder.Property(c => c.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(c => c.IsArchived).HasColumnName("is_archived").IsRequired();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.HouseholdId, c.Kind, c.DisplayOrder })
            .HasDatabaseName("ix_categories_household_kind");
    }
}

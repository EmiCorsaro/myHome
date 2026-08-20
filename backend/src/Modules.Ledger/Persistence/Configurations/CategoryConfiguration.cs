using MyHome.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Category"/> to the <c>ledger.categories</c> table.
/// </summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
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

        // Restrict and not Cascade: deleting a parent must not take its children's history.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.HouseholdId, c.Kind, c.DisplayOrder })
            .HasDatabaseName("ix_categories_household_kind");
    }
}

using MyHome.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <summary>Shadow property holding the name in lower case, kept by the database.</summary>
    internal const string NormalizedNameProperty = "NormalizedName";

    /// <summary>Column behind <see cref="NormalizedNameProperty"/>.</summary>
    private const string NormalizedNameColumn = "normalized_name";

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
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.HouseholdId, c.Kind, c.DisplayOrder })
            .HasDatabaseName("ix_categories_household_kind");

        // A household uses a name once, whatever its case, whatever the kind of the category and
        // whether or not it is archived. That is exactly the scope CategoryRegistrar checks before
        // inserting; this index is what makes it true when two members press save at the same
        // instant and both checks came back clean.
        //
        // A stored generated column and not an expression index: EF Core models columns, so this
        // way the rule lives in the model, travels to the migration on its own and is visible to
        // any provider instead of hiding in hand-written SQL.
        builder.Property<string>(NormalizedNameProperty)
            .HasColumnName(NormalizedNameColumn)
            .HasMaxLength(80)
            .HasComputedColumnSql("lower(name)", stored: true);

        builder.HasIndex(nameof(Category.HouseholdId), NormalizedNameProperty)
            .IsUnique()
            .HasDatabaseName("ux_categories_household_name");
    }
}

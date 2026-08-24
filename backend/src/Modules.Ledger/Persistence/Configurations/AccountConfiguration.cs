using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Account"/> to the <c>ledger.accounts</c> table.
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        // No foreign key to the households table: a cross-schema constraint is exactly what would
        // make extracting this module expensive later.
        builder.Property(a => a.HouseholdId).HasColumnName("household_id").IsRequired();

        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        // Enums as text. An ad-hoc query reads "checking" instead of 1, and reordering the enum
        // cannot silently reinterpret existing rows.
        builder.Property(a => a.Type)
            .HasColumnName("type")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasConversion(currency => currency.Value, value => CurrencyCode.Parse(value))
            .IsRequired();

        builder.Property(a => a.IsTracked).HasColumnName("is_tracked").IsRequired();

        builder.Property(a => a.MinimumBufferTarget)
            .HasColumnName("minimum_buffer_target")
            .HasPrecision(19, 4);

        builder.Property(a => a.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(a => a.IsArchived).HasColumnName("is_archived").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        // Every query in the module filters by household first.
        builder.HasIndex(a => new { a.HouseholdId, a.DisplayOrder }).HasDatabaseName("ix_accounts_household");
    }
}

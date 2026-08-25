using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id")
            .UseHiLo(LedgerDbContext.KeySequence, LedgerDbContext.Schema);

        builder.Property(a => a.PublicId).HasColumnName("public_id").IsRequired();
        builder.HasIndex(a => a.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_accounts_public_id");

        builder.Property(a => a.HouseholdId).HasColumnName("household_id").IsRequired();

        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

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

        builder.HasIndex(a => new { a.HouseholdId, a.DisplayOrder }).HasDatabaseName("ix_accounts_household");
    }
}

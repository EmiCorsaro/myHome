using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <summary>Shadow property holding the name in lower case, kept by the database.</summary>
    internal const string NormalizedNameProperty = "NormalizedName";

    /// <summary>Column behind <see cref="NormalizedNameProperty"/>.</summary>
    private const string NormalizedNameColumn = "normalized_name";

    /// <summary>
    /// Rows the unique name index covers: the accounts a household actually holds.
    /// </summary>
    /// <remarks>
    /// The accounts the system keeps to classify income and expense are left out on purpose. A
    /// user can neither create nor see them (RF-9), so their names are not part of the household's
    /// namespace, and the ledger creates one of each per currency — which the index would
    /// otherwise refuse the day a household holds two currencies.
    /// </remarks>
    private const string RealAccountsOnly = "\"type\" NOT IN ('Income', 'Expense')";

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

        // A household uses an account name once, whatever its case and whether or not the account
        // is archived. That is exactly the scope AccountRegistrar checks before inserting; this
        // index is what makes it true when two members press save at the same instant and both
        // checks came back clean.
        //
        // A stored generated column and not an expression index: EF Core models columns, so this
        // way the rule lives in the model, travels to the migration on its own and is visible to
        // any provider instead of hiding in hand-written SQL.
        builder.Property<string>(NormalizedNameProperty)
            .HasColumnName(NormalizedNameColumn)
            .HasMaxLength(120)
            .HasComputedColumnSql("lower(name)", stored: true);

        builder.HasIndex(nameof(Account.HouseholdId), NormalizedNameProperty)
            .IsUnique()
            .HasFilter(RealAccountsOnly)
            .HasDatabaseName("ux_accounts_household_name");
    }
}

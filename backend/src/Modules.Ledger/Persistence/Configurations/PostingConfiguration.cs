using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Posting"/> to the <c>ledger.postings</c> table.
/// </summary>
/// <remarks>
/// Every figure in the product is computed from this table, so amounts are <c>numeric(19,4)</c>:
/// decimal in the database as well as in the domain. A <c>double precision</c> column would put
/// the rounding error straight back into the one place <see cref="Money"/> keeps it out of.
/// </remarks>
internal sealed class PostingConfiguration : IEntityTypeConfiguration<Posting>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Posting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("postings");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.JournalEntryId).HasColumnName("journal_entry_id").IsRequired();
        builder.Property(p => p.AccountId).HasColumnName("account_id").IsRequired();

        builder.Property(p => p.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, Money.OperatingScale)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasConversion(currency => currency.Value, value => CurrencyCode.Parse(value))
            .IsRequired();

        builder.Property(p => p.CategoryId).HasColumnName("category_id");
        builder.Property(p => p.MemberId).HasColumnName("member_id");

        builder.Property(p => p.FxRate)
            .HasColumnName("fx_rate")
            .HasPrecision(19, 8)
            .IsRequired();

        builder.Property(p => p.AmountBase)
            .HasColumnName("amount_base")
            .HasPrecision(19, Money.OperatingScale)
            .IsRequired();

        // Computed from Amount and Currency.
        builder.Ignore(p => p.Money);

        // Accounts with history are archived, never deleted: losing one side of an entry leaves
        // the ledger permanently unbalanced.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.AccountId).HasDatabaseName("ix_postings_account");
        builder.HasIndex(p => p.CategoryId).HasDatabaseName("ix_postings_category");
    }
}

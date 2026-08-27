using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyHome.Modules.Ledger.Persistence.Configurations;

internal sealed class PostingConfiguration : IEntityTypeConfiguration<Posting>
{
    public void Configure(EntityTypeBuilder<Posting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("postings");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id")
            .UseHiLo(LedgerDbContext.KeySequence, LedgerDbContext.Schema);
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

        builder.Ignore(p => p.Money);

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

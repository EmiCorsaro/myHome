using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyHome.Modules.Ledger.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyHome.Modules.Ledger.Persistence.Migrations
{
    [DbContext(typeof(LedgerDbContext))]
    partial class LedgerDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("ledger")
                .HasAnnotation("ProductVersion", "10.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseHiLo(modelBuilder, "key_sequence", "ledger");

            modelBuilder.HasSequence("key_sequence", "ledger")
                .IncrementsBy(10);

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.Account", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "ledger");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)")
                        .HasColumnName("currency");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<int>("HouseholdId")
                        .HasColumnType("integer")
                        .HasColumnName("household_id");

                    b.Property<bool>("IsArchived")
                        .HasColumnType("boolean")
                        .HasColumnName("is_archived");

                    b.Property<bool>("IsTracked")
                        .HasColumnType("boolean")
                        .HasColumnName("is_tracked");

                    b.Property<decimal?>("MinimumBufferTarget")
                        .HasPrecision(19, 4)
                        .HasColumnType("numeric(19,4)")
                        .HasColumnName("minimum_buffer_target");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(120)
                        .HasColumnType("character varying(120)")
                        .HasColumnName("name");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("type");

                    b.HasKey("Id");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_accounts_public_id");

                    b.HasIndex("HouseholdId", "DisplayOrder")
                        .HasDatabaseName("ix_accounts_household");

                    b.ToTable("accounts", "ledger");
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.Category", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "ledger");

                    b.Property<int>("ColorIndex")
                        .HasColumnType("integer")
                        .HasColumnName("color_index");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<int>("HouseholdId")
                        .HasColumnType("integer")
                        .HasColumnName("household_id");

                    b.Property<bool>("IsArchived")
                        .HasColumnType("boolean")
                        .HasColumnName("is_archived");

                    b.Property<string>("Kind")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("kind");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(80)
                        .HasColumnType("character varying(80)")
                        .HasColumnName("name");

                    b.Property<int?>("ParentId")
                        .HasColumnType("integer")
                        .HasColumnName("parent_id");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.HasKey("Id");

                    b.HasIndex("ParentId");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_categories_public_id");

                    b.HasIndex("HouseholdId", "Kind", "DisplayOrder")
                        .HasDatabaseName("ix_categories_household_kind");

                    b.ToTable("categories", "ledger");
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.CategoryBudget", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "ledger");

                    b.Property<decimal>("Amount")
                        .HasPrecision(19, 4)
                        .HasColumnType("numeric(19,4)")
                        .HasColumnName("amount");

                    b.Property<int>("CategoryId")
                        .HasColumnType("integer")
                        .HasColumnName("category_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)")
                        .HasColumnName("currency");

                    b.Property<int>("HouseholdId")
                        .HasColumnType("integer")
                        .HasColumnName("household_id");

                    b.Property<DateOnly>("PeriodStart")
                        .HasColumnType("date")
                        .HasColumnName("period_start");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.Property<string>("Scope")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("scope");

                    b.HasKey("Id");

                    b.HasIndex("CategoryId");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_category_budgets_public_id");

                    b.HasIndex("HouseholdId", "PeriodStart")
                        .HasDatabaseName("ix_category_budgets_household_period");

                    b.HasIndex("HouseholdId", "CategoryId", "PeriodStart")
                        .IsUnique()
                        .HasDatabaseName("ux_category_budgets_period");

                    b.ToTable("category_budgets", "ledger", t =>
                        {
                            t.HasCheckConstraint("ck_category_budgets_period_start_is_first", "date_part('day', period_start) = 1");
                        });
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.Income", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "ledger");

                    b.Property<int>("AccountId")
                        .HasColumnType("integer")
                        .HasColumnName("account_id");

                    b.Property<decimal>("Amount")
                        .HasPrecision(19, 4)
                        .HasColumnType("numeric(19,4)")
                        .HasColumnName("amount");

                    b.Property<int>("CategoryId")
                        .HasColumnType("integer")
                        .HasColumnName("category_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)")
                        .HasColumnName("currency");

                    b.Property<int>("DayOfMonth")
                        .HasColumnType("integer")
                        .HasColumnName("day_of_month");

                    b.Property<int>("DayToleranceDays")
                        .HasColumnType("integer")
                        .HasColumnName("day_tolerance_days");

                    b.Property<int>("HouseholdId")
                        .HasColumnType("integer")
                        .HasColumnName("household_id");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<string>("Periodicity")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("periodicity");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.Property<string>("Source")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("source");

                    b.Property<DateOnly>("StartsOn")
                        .HasColumnType("date")
                        .HasColumnName("starts_on");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("CategoryId");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_incomes_public_id");

                    b.HasIndex("HouseholdId", "IsActive")
                        .HasDatabaseName("ix_incomes_household");

                    b.HasIndex("HouseholdId", "StartsOn")
                        .HasDatabaseName("ix_incomes_household_starts_on");

                    b.ToTable("incomes", "ledger");
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.JournalEntry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "ledger");

                    b.Property<string>("ClientMutationId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("client_mutation_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("description");

                    b.Property<int>("HouseholdId")
                        .HasColumnType("integer")
                        .HasColumnName("household_id");

                    b.Property<string>("Kind")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("kind");

                    b.Property<DateOnly>("OccurredOn")
                        .HasColumnType("date")
                        .HasColumnName("occurred_on");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.Property<int?>("RecurringRuleId")
                        .HasColumnType("integer")
                        .HasColumnName("recurring_rule_id");

                    b.HasKey("Id");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_journal_entries_public_id");

                    b.HasIndex("RecurringRuleId");

                    b.HasIndex("HouseholdId", "ClientMutationId")
                        .IsUnique()
                        .HasDatabaseName("ux_journal_entries_client_mutation")
                        .HasFilter("client_mutation_id IS NOT NULL");

                    b.HasIndex("HouseholdId", "OccurredOn")
                        .HasDatabaseName("ix_journal_entries_household_date");

                    b.ToTable("journal_entries", "ledger");
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.PlannedMovement", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "ledger");

                    b.Property<int>("AccountId")
                        .HasColumnType("integer")
                        .HasColumnName("account_id");

                    b.Property<decimal?>("ActualAmount")
                        .HasPrecision(19, 4)
                        .HasColumnType("numeric(19,4)")
                        .HasColumnName("actual_amount");

                    b.Property<string>("AmountMode")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("amount_mode");

                    b.Property<int>("CategoryId")
                        .HasColumnType("integer")
                        .HasColumnName("category_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)")
                        .HasColumnName("currency");

                    b.Property<int>("DayToleranceDays")
                        .HasColumnType("integer")
                        .HasColumnName("day_tolerance_days");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("description");

                    b.Property<DateOnly>("DueDate")
                        .HasColumnType("date")
                        .HasColumnName("due_date");

                    b.Property<decimal>("ExpectedAmount")
                        .HasPrecision(19, 4)
                        .HasColumnType("numeric(19,4)")
                        .HasColumnName("expected_amount");

                    b.Property<int>("HouseholdId")
                        .HasColumnType("integer")
                        .HasColumnName("household_id");

                    b.Property<int?>("IncomeId")
                        .HasColumnType("integer")
                        .HasColumnName("income_id");

                    b.Property<int?>("JournalEntryId")
                        .HasColumnType("integer")
                        .HasColumnName("journal_entry_id");

                    b.Property<string>("Kind")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("kind");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.Property<int?>("RuleId")
                        .HasColumnType("integer")
                        .HasColumnName("rule_id");

                    b.Property<DateTimeOffset?>("SettledAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("settled_at");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("status");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("CategoryId");

                    b.HasIndex("JournalEntryId")
                        .IsUnique()
                        .HasDatabaseName("ux_planned_movements_entry")
                        .HasFilter("journal_entry_id IS NOT NULL");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_planned_movements_public_id");

                    b.HasIndex("IncomeId", "DueDate")
                        .IsUnique()
                        .HasDatabaseName("ux_planned_movements_income_due")
                        .HasFilter("income_id IS NOT NULL");

                    b.HasIndex("RuleId", "DueDate")
                        .IsUnique()
                        .HasDatabaseName("ux_planned_movements_rule_due")
                        .HasFilter("rule_id IS NOT NULL");

                    b.HasIndex("HouseholdId", "DueDate", "Status")
                        .HasDatabaseName("ix_planned_movements_household_due");

                    b.HasIndex("HouseholdId", "AccountId", "CategoryId", "DueDate")
                        .HasDatabaseName("ix_planned_movements_match");

                    b.ToTable("planned_movements", "ledger", t =>
                        {
                            t.HasCheckConstraint("ck_planned_movements_settlement_complete", "num_nonnulls(journal_entry_id, actual_amount, settled_at) IN (0, 3)");

                            t.HasCheckConstraint("ck_planned_movements_single_origin", "num_nonnulls(rule_id, income_id) <= 1");
                        });
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.Posting", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "ledger");

                    b.Property<int>("AccountId")
                        .HasColumnType("integer")
                        .HasColumnName("account_id");

                    b.Property<decimal>("Amount")
                        .HasPrecision(19, 4)
                        .HasColumnType("numeric(19,4)")
                        .HasColumnName("amount");

                    b.Property<decimal>("AmountBase")
                        .HasPrecision(19, 4)
                        .HasColumnType("numeric(19,4)")
                        .HasColumnName("amount_base");

                    b.Property<int?>("CategoryId")
                        .HasColumnType("integer")
                        .HasColumnName("category_id");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)")
                        .HasColumnName("currency");

                    b.Property<decimal>("FxRate")
                        .HasPrecision(19, 8)
                        .HasColumnType("numeric(19,8)")
                        .HasColumnName("fx_rate");

                    b.Property<int>("JournalEntryId")
                        .HasColumnType("integer")
                        .HasColumnName("journal_entry_id");

                    b.Property<int?>("MemberId")
                        .HasColumnType("integer")
                        .HasColumnName("member_id");

                    b.HasKey("Id");

                    b.HasIndex("AccountId")
                        .HasDatabaseName("ix_postings_account");

                    b.HasIndex("CategoryId")
                        .HasDatabaseName("ix_postings_category");

                    b.HasIndex("JournalEntryId");

                    b.ToTable("postings", "ledger");
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.RecurringRule", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "ledger");

                    b.Property<int>("AccountId")
                        .HasColumnType("integer")
                        .HasColumnName("account_id");

                    b.Property<decimal>("Amount")
                        .HasPrecision(19, 4)
                        .HasColumnType("numeric(19,4)")
                        .HasColumnName("amount");

                    b.Property<string>("AmountMode")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("amount_mode");

                    b.Property<int>("CategoryId")
                        .HasColumnType("integer")
                        .HasColumnName("category_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)")
                        .HasColumnName("currency");

                    b.Property<int>("DayOfMonth")
                        .HasColumnType("integer")
                        .HasColumnName("day_of_month");

                    b.Property<int>("DayToleranceDays")
                        .HasColumnType("integer")
                        .HasColumnName("day_tolerance_days");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("description");

                    b.Property<DateOnly?>("EndsOn")
                        .HasColumnType("date")
                        .HasColumnName("ends_on");

                    b.Property<string>("Frequency")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("frequency");

                    b.Property<int>("HouseholdId")
                        .HasColumnType("integer")
                        .HasColumnName("household_id");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("Kind")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("kind");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.Property<DateOnly>("StartsOn")
                        .HasColumnType("date")
                        .HasColumnName("starts_on");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("CategoryId");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_recurring_rules_public_id");

                    b.HasIndex("HouseholdId", "IsActive")
                        .HasDatabaseName("ix_recurring_rules_household");

                    b.ToTable("recurring_rules", "ledger");
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.Category", b =>
                {
                    b.HasOne("MyHome.Modules.Ledger.Domain.Category", null)
                        .WithMany()
                        .HasForeignKey("ParentId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.CategoryBudget", b =>
                {
                    b.HasOne("MyHome.Modules.Ledger.Domain.Category", null)
                        .WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.Income", b =>
                {
                    b.HasOne("MyHome.Modules.Ledger.Domain.Account", null)
                        .WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("MyHome.Modules.Ledger.Domain.Category", null)
                        .WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.JournalEntry", b =>
                {
                    b.HasOne("MyHome.Modules.Ledger.Domain.RecurringRule", "RecurringRule")
                        .WithMany()
                        .HasForeignKey("RecurringRuleId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("RecurringRule");
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.PlannedMovement", b =>
                {
                    b.HasOne("MyHome.Modules.Ledger.Domain.Account", null)
                        .WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("MyHome.Modules.Ledger.Domain.Category", null)
                        .WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("MyHome.Modules.Ledger.Domain.Income", null)
                        .WithMany()
                        .HasForeignKey("IncomeId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("MyHome.Modules.Ledger.Domain.JournalEntry", "JournalEntry")
                        .WithMany()
                        .HasForeignKey("JournalEntryId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("MyHome.Modules.Ledger.Domain.RecurringRule", null)
                        .WithMany()
                        .HasForeignKey("RuleId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("JournalEntry");
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.Posting", b =>
                {
                    b.HasOne("MyHome.Modules.Ledger.Domain.Account", null)
                        .WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("MyHome.Modules.Ledger.Domain.Category", null)
                        .WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("MyHome.Modules.Ledger.Domain.JournalEntry", null)
                        .WithMany("Postings")
                        .HasForeignKey("JournalEntryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.RecurringRule", b =>
                {
                    b.HasOne("MyHome.Modules.Ledger.Domain.Account", null)
                        .WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("MyHome.Modules.Ledger.Domain.Category", null)
                        .WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("MyHome.Modules.Ledger.Domain.JournalEntry", b =>
                {
                    b.Navigation("Postings");
                });
#pragma warning restore 612, 618
        }
    }
}

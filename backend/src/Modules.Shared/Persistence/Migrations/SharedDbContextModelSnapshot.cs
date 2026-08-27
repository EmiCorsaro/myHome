using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyHome.Modules.Shared.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyHome.Modules.Shared.Persistence.Migrations
{
    [DbContext(typeof(SharedDbContext))]
    partial class SharedDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("shared")
                .HasAnnotation("ProductVersion", "10.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseHiLo(modelBuilder, "key_sequence", "shared");

            modelBuilder.HasSequence("key_sequence", "shared")
                .IncrementsBy(10);

            modelBuilder.Entity("MyHome.Modules.Shared.Domain.Household", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "shared");

                    b.Property<string>("BaseCurrency")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)")
                        .HasColumnName("base_currency");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(120)
                        .HasColumnType("character varying(120)")
                        .HasColumnName("name");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.Property<string>("TimeZoneId")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("time_zone_id");

                    b.HasKey("Id");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_households_public_id");

                    b.ToTable("households", "shared");
                });

            modelBuilder.Entity("MyHome.Modules.Shared.Domain.HouseholdMember", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseHiLo(b.Property<int>("Id"), "key_sequence", "shared");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(80)
                        .HasColumnType("character varying(80)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<int>("HouseholdId")
                        .HasColumnType("integer")
                        .HasColumnName("household_id");

                    b.Property<DateOnly>("JoinedAt")
                        .HasColumnType("date")
                        .HasColumnName("joined_at");

                    b.Property<Guid>("PublicId")
                        .HasColumnType("uuid")
                        .HasColumnName("public_id");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("role");

                    b.Property<Guid?>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id");

                    b.HasIndex("PublicId")
                        .IsUnique()
                        .HasDatabaseName("ux_household_members_public_id");

                    b.HasIndex("UserId")
                        .IsUnique()
                        .HasFilter("user_id IS NOT NULL");

                    b.HasIndex("HouseholdId", "DisplayOrder");

                    b.ToTable("household_members", "shared");
                });

            modelBuilder.Entity("MyHome.Modules.Shared.Domain.HouseholdMember", b =>
                {
                    b.HasOne("MyHome.Modules.Shared.Domain.Household", null)
                        .WithMany("Members")
                        .HasForeignKey("HouseholdId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyHome.Modules.Shared.Domain.Household", b =>
                {
                    b.Navigation("Members");
                });
#pragma warning restore 612, 618
        }
    }
}

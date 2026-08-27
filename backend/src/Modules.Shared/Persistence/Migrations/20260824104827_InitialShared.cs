using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHome.Modules.Shared.Persistence.Migrations
{
    public partial class InitialShared : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.CreateSequence(
                name: "key_sequence",
                schema: "shared",
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "households",
                schema: "shared",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    base_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_households", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "household_members",
                schema: "shared",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_household_members_households_household_id",
                        column: x => x.household_id,
                        principalSchema: "shared",
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_household_members_household_id_display_order",
                schema: "shared",
                table: "household_members",
                columns: new[] { "household_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_household_members_user_id",
                schema: "shared",
                table: "household_members",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_household_members_public_id",
                schema: "shared",
                table: "household_members",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_households_public_id",
                schema: "shared",
                table: "households",
                column: "public_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "household_members",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "households",
                schema: "shared");

            migrationBuilder.DropSequence(
                name: "key_sequence",
                schema: "shared");
        }
    }
}

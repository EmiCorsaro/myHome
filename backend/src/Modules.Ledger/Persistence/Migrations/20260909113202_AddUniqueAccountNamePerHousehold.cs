using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHome.Modules.Ledger.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueAccountNamePerHousehold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                schema: "ledger",
                table: "accounts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                computedColumnSql: "lower(name)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ux_accounts_household_name",
                schema: "ledger",
                table: "accounts",
                columns: new[] { "household_id", "normalized_name" },
                unique: true,
                filter: "\"type\" NOT IN ('Income', 'Expense')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_accounts_household_name",
                schema: "ledger",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                schema: "ledger",
                table: "accounts");
        }
    }
}

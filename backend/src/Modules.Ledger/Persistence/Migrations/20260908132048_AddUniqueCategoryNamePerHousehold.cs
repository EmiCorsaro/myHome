using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHome.Modules.Ledger.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCategoryNamePerHousehold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                schema: "ledger",
                table: "categories",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                computedColumnSql: "lower(name)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ux_categories_household_name",
                schema: "ledger",
                table: "categories",
                columns: new[] { "household_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_categories_household_name",
                schema: "ledger",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                schema: "ledger",
                table: "categories");
        }
    }
}

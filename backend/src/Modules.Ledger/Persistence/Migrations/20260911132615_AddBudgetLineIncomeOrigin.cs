using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHome.Modules.Ledger.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Purely additive, so nothing already written is migrated: the column arrives nullable and
    /// every existing row is an expense line, which is exactly the case that must carry no origin.
    /// The check constraint therefore holds over the old data the moment it is created.
    /// </remarks>
    public partial class AddBudgetLineIncomeOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origin",
                schema: "ledger",
                table: "budget_lines",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_budget_lines_origin_matches_sign",
                schema: "ledger",
                table: "budget_lines",
                sql: "(sign = 'Income' AND origin IS NOT NULL) OR (sign = 'Expense' AND origin IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_budget_lines_origin_matches_sign",
                schema: "ledger",
                table: "budget_lines");

            migrationBuilder.DropColumn(
                name: "origin",
                schema: "ledger",
                table: "budget_lines");
        }
    }
}

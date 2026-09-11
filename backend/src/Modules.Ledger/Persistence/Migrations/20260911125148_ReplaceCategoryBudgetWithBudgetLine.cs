using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHome.Modules.Ledger.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCategoryBudgetWithBudgetLine : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// The old table is dropped rather than altered. A budget line now declares an account, a
        /// sign and an amount mode, and the rows of <c>category_budgets</c> carry none of the
        /// three: there is no account to derive from them, and choosing one on their behalf would
        /// be inventing a decision the household never made. The only rows that ever existed are
        /// the ones the seeder writes into a development database, so nothing typed is lost.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_budgets",
                schema: "ledger");

            migrationBuilder.CreateTable(
                name: "budget_lines",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    sign = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_lines", x => x.id);
                    table.CheckConstraint("ck_budget_lines_amount_is_positive", "amount > 0");
                    table.CheckConstraint("ck_budget_lines_period_start_is_first", "date_part('day', period_start) = 1");
                    table.ForeignKey(
                        name: "FK_budget_lines_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "ledger",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_lines_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "ledger",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budget_lines_account_id",
                schema: "ledger",
                table: "budget_lines",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_budget_lines_category_id",
                schema: "ledger",
                table: "budget_lines",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_lines_household_period",
                schema: "ledger",
                table: "budget_lines",
                columns: new[] { "household_id", "period_start" });

            migrationBuilder.CreateIndex(
                name: "ux_budget_lines_period",
                schema: "ledger",
                table: "budget_lines",
                columns: new[] { "household_id", "category_id", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_budget_lines_public_id",
                schema: "ledger",
                table: "budget_lines",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_lines",
                schema: "ledger");

            migrationBuilder.CreateTable(
                name: "category_budgets",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    household_id = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_budgets", x => x.id);
                    table.CheckConstraint("ck_category_budgets_period_start_is_first", "date_part('day', period_start) = 1");
                    table.ForeignKey(
                        name: "FK_category_budgets_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "ledger",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_category_budgets_category_id",
                schema: "ledger",
                table: "category_budgets",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_category_budgets_household_period",
                schema: "ledger",
                table: "category_budgets",
                columns: new[] { "household_id", "period_start" });

            migrationBuilder.CreateIndex(
                name: "ux_category_budgets_period",
                schema: "ledger",
                table: "category_budgets",
                columns: new[] { "household_id", "category_id", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_category_budgets_public_id",
                schema: "ledger",
                table: "category_budgets",
                column: "public_id",
                unique: true);
        }
    }
}

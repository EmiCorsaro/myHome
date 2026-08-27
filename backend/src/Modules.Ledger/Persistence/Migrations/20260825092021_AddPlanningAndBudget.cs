using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHome.Modules.Ledger.Persistence.Migrations
{
    public partial class AddPlanningAndBudget : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "amount_mode",
                schema: "ledger",
                table: "recurring_rules",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Fixed");

            migrationBuilder.AddColumn<int>(
                name: "day_tolerance_days",
                schema: "ledger",
                table: "recurring_rules",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ends_on",
                schema: "ledger",
                table: "recurring_rules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "day_tolerance_days",
                schema: "ledger",
                table: "incomes",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateTable(
                name: "category_budgets",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "planned_movements",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<int>(type: "integer", nullable: false),
                    rule_id = table.Column<int>(type: "integer", nullable: true),
                    income_id = table.Column<int>(type: "integer", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    day_tolerance_days = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    journal_entry_id = table.Column<int>(type: "integer", nullable: true),
                    actual_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planned_movements", x => x.id);
                    table.CheckConstraint("ck_planned_movements_settlement_complete", "num_nonnulls(journal_entry_id, actual_amount, settled_at) IN (0, 3)");
                    table.CheckConstraint("ck_planned_movements_single_origin", "num_nonnulls(rule_id, income_id) <= 1");
                    table.ForeignKey(
                        name: "FK_planned_movements_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "ledger",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planned_movements_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "ledger",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planned_movements_incomes_income_id",
                        column: x => x.income_id,
                        principalSchema: "ledger",
                        principalTable: "incomes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_planned_movements_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalSchema: "ledger",
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_planned_movements_recurring_rules_rule_id",
                        column: x => x.rule_id,
                        principalSchema: "ledger",
                        principalTable: "recurring_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateIndex(
                name: "IX_planned_movements_account_id",
                schema: "ledger",
                table: "planned_movements",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_planned_movements_category_id",
                schema: "ledger",
                table: "planned_movements",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_planned_movements_household_due",
                schema: "ledger",
                table: "planned_movements",
                columns: new[] { "household_id", "due_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_planned_movements_match",
                schema: "ledger",
                table: "planned_movements",
                columns: new[] { "household_id", "account_id", "category_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ux_planned_movements_entry",
                schema: "ledger",
                table: "planned_movements",
                column: "journal_entry_id",
                unique: true,
                filter: "journal_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_planned_movements_income_due",
                schema: "ledger",
                table: "planned_movements",
                columns: new[] { "income_id", "due_date" },
                unique: true,
                filter: "income_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_planned_movements_public_id",
                schema: "ledger",
                table: "planned_movements",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_planned_movements_rule_due",
                schema: "ledger",
                table: "planned_movements",
                columns: new[] { "rule_id", "due_date" },
                unique: true,
                filter: "rule_id IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_budgets",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "planned_movements",
                schema: "ledger");

            migrationBuilder.DropColumn(
                name: "amount_mode",
                schema: "ledger",
                table: "recurring_rules");

            migrationBuilder.DropColumn(
                name: "day_tolerance_days",
                schema: "ledger",
                table: "recurring_rules");

            migrationBuilder.DropColumn(
                name: "ends_on",
                schema: "ledger",
                table: "recurring_rules");

            migrationBuilder.DropColumn(
                name: "day_tolerance_days",
                schema: "ledger",
                table: "incomes");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHome.Modules.Ledger.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOpeningBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "opening_account_id",
                schema: "ledger",
                table: "journal_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_journal_entries_opening_account",
                schema: "ledger",
                table: "journal_entries",
                column: "opening_account_id",
                unique: true,
                filter: "opening_account_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_accounts_opening_account_id",
                schema: "ledger",
                table: "journal_entries",
                column: "opening_account_id",
                principalSchema: "ledger",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_accounts_opening_account_id",
                schema: "ledger",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "ux_journal_entries_opening_account",
                schema: "ledger",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "opening_account_id",
                schema: "ledger",
                table: "journal_entries");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHome.Modules.Ledger.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_voided",
                schema: "ledger",
                table: "journal_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "reversal_of_entry_id",
                schema: "ledger",
                table: "journal_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_journal_entries_reversal_of_entry",
                schema: "ledger",
                table: "journal_entries",
                column: "reversal_of_entry_id",
                unique: true,
                filter: "reversal_of_entry_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_journal_entries_reversal_of_entry_id",
                schema: "ledger",
                table: "journal_entries",
                column: "reversal_of_entry_id",
                principalSchema: "ledger",
                principalTable: "journal_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_journal_entries_reversal_of_entry_id",
                schema: "ledger",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "ux_journal_entries_reversal_of_entry",
                schema: "ledger",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "is_voided",
                schema: "ledger",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "reversal_of_entry_id",
                schema: "ledger",
                table: "journal_entries");
        }
    }
}

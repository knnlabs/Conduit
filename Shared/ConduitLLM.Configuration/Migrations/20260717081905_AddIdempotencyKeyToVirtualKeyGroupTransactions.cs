using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKeyToVirtualKeyGroupTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "VirtualKeyGroupTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_IdempotencyKey",
                table: "VirtualKeyGroupTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VirtualKeyGroupTransactions_IdempotencyKey",
                table: "VirtualKeyGroupTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "VirtualKeyGroupTransactions");
        }
    }
}

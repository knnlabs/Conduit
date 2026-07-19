using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddCachedTokenFieldsToRequestLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CachedInputTokens",
                table: "RequestLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CachedWriteTokens",
                table: "RequestLogs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedInputTokens",
                table: "RequestLogs");

            migrationBuilder.DropColumn(
                name: "CachedWriteTokens",
                table: "RequestLogs");
        }
    }
}

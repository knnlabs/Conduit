using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingMethodToRequestLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingMethod",
                table: "RequestLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderReportedCostUsd",
                table: "RequestLogs",
                type: "numeric(18,8)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingMethod",
                table: "RequestLogs");

            migrationBuilder.DropColumn(
                name: "ProviderReportedCostUsd",
                table: "RequestLogs");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCostTrustFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing providers to a 1.0 (pass-through) markup rather than 0.0, so that
            // enabling TrustProviderReportedCosts later never silently bills at zero.
            migrationBuilder.AddColumn<decimal>(
                name: "ProviderCostMarkupMultiplier",
                table: "Providers",
                type: "numeric",
                nullable: false,
                defaultValue: 1.0m);

            migrationBuilder.AddColumn<bool>(
                name: "TrustProviderReportedCosts",
                table: "Providers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderCostMarkupMultiplier",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "TrustProviderReportedCosts",
                table: "Providers");
        }
    }
}

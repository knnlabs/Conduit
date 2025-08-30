using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingModelIdentifierColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "ModelIdentifiers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxInputTokens",
                table: "ModelIdentifiers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxOutputTokens",
                table: "ModelIdentifiers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModelCostId",
                table: "ModelIdentifiers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderVariation",
                table: "ModelIdentifiers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QualityScore",
                table: "ModelIdentifiers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpeedScore",
                table: "ModelIdentifiers",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifiers_ModelCostId",
                table: "ModelIdentifiers",
                column: "ModelCostId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelIdentifiers_ModelCosts_ModelCostId",
                table: "ModelIdentifiers",
                column: "ModelCostId",
                principalTable: "ModelCosts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelIdentifiers_ModelCosts_ModelCostId",
                table: "ModelIdentifiers");

            migrationBuilder.DropIndex(
                name: "IX_ModelIdentifiers_ModelCostId",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "MaxInputTokens",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "MaxOutputTokens",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "ModelCostId",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "ProviderVariation",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "QualityScore",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "SpeedScore",
                table: "ModelIdentifiers");
        }
    }
}

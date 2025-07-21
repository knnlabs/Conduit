using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddCachedTokenCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CachedInputTokenCost",
                table: "ModelCosts",
                type: "numeric(18,10)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CachedInputWriteCost",
                table: "ModelCosts",
                type: "numeric(18,10)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedInputTokenCost",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "CachedInputWriteCost",
                table: "ModelCosts");
        }
    }
}

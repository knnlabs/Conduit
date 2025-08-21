using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddApiParametersToModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiParameters",
                table: "ModelSeries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiParameters",
                table: "Models",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiParameters",
                table: "ModelSeries");

            migrationBuilder.DropColumn(
                name: "ApiParameters",
                table: "Models");
        }
    }
}

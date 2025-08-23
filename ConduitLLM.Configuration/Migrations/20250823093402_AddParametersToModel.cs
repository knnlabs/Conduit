using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddParametersToModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiParameters",
                table: "ModelSeries");

            migrationBuilder.DropColumn(
                name: "ApiParameters",
                table: "ModelProviderMappings");

            migrationBuilder.RenameColumn(
                name: "ApiParameters",
                table: "Models",
                newName: "Parameters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Parameters",
                table: "Models",
                newName: "ApiParameters");

            migrationBuilder.AddColumn<string>(
                name: "ApiParameters",
                table: "ModelSeries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiParameters",
                table: "ModelProviderMappings",
                type: "text",
                nullable: true);
        }
    }
}

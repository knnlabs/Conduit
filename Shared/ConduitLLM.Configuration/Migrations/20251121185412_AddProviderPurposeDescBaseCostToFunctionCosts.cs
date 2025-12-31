using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderPurposeDescBaseCostToFunctionCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseCost",
                table: "FunctionCosts",
                type: "numeric(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "FunctionCosts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderType",
                table: "FunctionCosts",
                type: "integer",
                nullable: false,
                defaultValue: 1); // Default to Exa for existing records

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "FunctionCosts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseCost",
                table: "FunctionCosts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "FunctionCosts");

            migrationBuilder.DropColumn(
                name: "ProviderType",
                table: "FunctionCosts");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "FunctionCosts");
        }
    }
}

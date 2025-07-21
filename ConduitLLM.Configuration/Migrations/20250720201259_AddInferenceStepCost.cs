using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddInferenceStepCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPerInferenceStep",
                table: "ModelCosts",
                type: "numeric(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultInferenceSteps",
                table: "ModelCosts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostPerInferenceStep",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "DefaultInferenceSteps",
                table: "ModelCosts");
        }
    }
}

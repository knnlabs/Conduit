using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeprecatedModelCostFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostPerInferenceStep",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "DefaultInferenceSteps",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "ImageCostPerImage",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "ImageQualityMultipliers",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "ImageResolutionMultipliers",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "VideoCostPerSecond",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "VideoResolutionMultipliers",
                table: "ModelCosts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<decimal>(
                name: "ImageCostPerImage",
                table: "ModelCosts",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageQualityMultipliers",
                table: "ModelCosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageResolutionMultipliers",
                table: "ModelCosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VideoCostPerSecond",
                table: "ModelCosts",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoResolutionMultipliers",
                table: "ModelCosts",
                type: "text",
                nullable: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioCapabilitiesAndCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SupportsSpeechToText",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsTextToSpeech",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AudioCostPerMinute",
                table: "ModelCosts",
                type: "numeric(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AudioCostPerThousandCharacters",
                table: "ModelCosts",
                type: "numeric(18,8)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportsSpeechToText",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsTextToSpeech",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "AudioCostPerMinute",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "AudioCostPerThousandCharacters",
                table: "ModelCosts");
        }
    }
}

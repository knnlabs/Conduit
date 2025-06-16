using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddModelCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultCapabilityType",
                table: "ModelProviderMappings",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "ModelProviderMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SupportedFormats",
                table: "ModelProviderMappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportedLanguages",
                table: "ModelProviderMappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportedVoices",
                table: "ModelProviderMappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsAudioTranscription",
                table: "ModelProviderMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsRealtimeAudio",
                table: "ModelProviderMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsTextToSpeech",
                table: "ModelProviderMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsVision",
                table: "ModelProviderMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TokenizerType",
                table: "ModelProviderMappings",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AudioCostPerKCharacters",
                table: "ModelCosts",
                type: "decimal(18, 4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AudioCostPerMinute",
                table: "ModelCosts",
                type: "decimal(18, 4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AudioInputCostPerMinute",
                table: "ModelCosts",
                type: "decimal(18, 4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AudioOutputCostPerMinute",
                table: "ModelCosts",
                type: "decimal(18, 4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCapabilityType",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportedFormats",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportedLanguages",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportedVoices",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsAudioTranscription",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsRealtimeAudio",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsTextToSpeech",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsVision",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "TokenizerType",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "AudioCostPerKCharacters",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "AudioCostPerMinute",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "AudioInputCostPerMinute",
                table: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "AudioOutputCostPerMinute",
                table: "ModelCosts");
        }
    }
}

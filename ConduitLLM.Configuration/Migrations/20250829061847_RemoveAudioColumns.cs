using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAudioColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop audio-related columns from ModelCapabilities table
            migrationBuilder.DropColumn(
                name: "SupportsAudioTranscription",
                table: "ModelCapabilities");
                
            migrationBuilder.DropColumn(
                name: "SupportsTextToSpeech",
                table: "ModelCapabilities");
                
            migrationBuilder.DropColumn(
                name: "SupportsRealtimeAudio",
                table: "ModelCapabilities");
                
            migrationBuilder.DropColumn(
                name: "SupportedVoices",
                table: "ModelCapabilities");
                
            migrationBuilder.DropColumn(
                name: "SupportedLanguages",
                table: "ModelCapabilities");
                
            migrationBuilder.DropColumn(
                name: "SupportedFormats",
                table: "ModelCapabilities");
                
            // Drop audio-related columns from ModelCosts table
            migrationBuilder.DropColumn(
                name: "AudioCostPerMinute",
                table: "ModelCosts");
                
            migrationBuilder.DropColumn(
                name: "AudioCostPerKCharacters",
                table: "ModelCosts");
                
            migrationBuilder.DropColumn(
                name: "AudioInputCostPerMinute",
                table: "ModelCosts");
                
            migrationBuilder.DropColumn(
                name: "AudioOutputCostPerMinute",
                table: "ModelCosts");

            // Drop audio-related tables that were previously created
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AudioCosts\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AudioProviderConfigs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AudioUsageLogs\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add audio columns if rolling back
            migrationBuilder.AddColumn<bool>(
                name: "SupportsAudioTranscription",
                table: "ModelCapabilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
                
            migrationBuilder.AddColumn<bool>(
                name: "SupportsTextToSpeech",
                table: "ModelCapabilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
                
            migrationBuilder.AddColumn<bool>(
                name: "SupportsRealtimeAudio",
                table: "ModelCapabilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
                
            migrationBuilder.AddColumn<string>(
                name: "SupportedVoices",
                table: "ModelCapabilities",
                type: "text",
                nullable: true);
                
            migrationBuilder.AddColumn<string>(
                name: "SupportedLanguages",
                table: "ModelCapabilities",
                type: "text",
                nullable: true);
                
            migrationBuilder.AddColumn<string>(
                name: "SupportedFormats",
                table: "ModelCapabilities",
                type: "text",
                nullable: true);
                
            // Re-add audio columns to ModelCosts if rolling back
            migrationBuilder.AddColumn<decimal>(
                name: "AudioCostPerMinute",
                table: "ModelCosts",
                type: "decimal(18,4)",
                nullable: true);
                
            migrationBuilder.AddColumn<decimal>(
                name: "AudioCostPerKCharacters",
                table: "ModelCosts",
                type: "decimal(18,4)",
                nullable: true);
                
            migrationBuilder.AddColumn<decimal>(
                name: "AudioInputCostPerMinute",
                table: "ModelCosts",
                type: "decimal(18,4)",
                nullable: true);
                
            migrationBuilder.AddColumn<decimal>(
                name: "AudioOutputCostPerMinute",
                table: "ModelCosts",
                type: "decimal(18,4)",
                nullable: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAudioToProviderTypeEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add temporary columns to hold the converted values
            migrationBuilder.AddColumn<int>(
                name: "ProviderTypeTemp",
                table: "AudioUsageLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderTypeTemp",
                table: "AudioCosts",
                type: "integer",
                nullable: true);

            // Convert string provider names to ProviderType enum values for AudioUsageLogs
            migrationBuilder.Sql(@"
                UPDATE ""AudioUsageLogs""
                SET ""ProviderTypeTemp"" = CASE 
                    WHEN LOWER(""Provider"") = 'openai' THEN 1
                    WHEN LOWER(""Provider"") = 'anthropic' THEN 2
                    WHEN LOWER(""Provider"") = 'azureopenai' THEN 3
                    WHEN LOWER(""Provider"") = 'gemini' THEN 4
                    WHEN LOWER(""Provider"") = 'vertexai' THEN 5
                    WHEN LOWER(""Provider"") = 'cohere' THEN 6
                    WHEN LOWER(""Provider"") = 'mistral' THEN 7
                    WHEN LOWER(""Provider"") = 'groq' THEN 8
                    WHEN LOWER(""Provider"") = 'ollama' THEN 9
                    WHEN LOWER(""Provider"") = 'replicate' THEN 10
                    WHEN LOWER(""Provider"") = 'fireworks' THEN 11
                    WHEN LOWER(""Provider"") = 'bedrock' THEN 12
                    WHEN LOWER(""Provider"") = 'huggingface' THEN 13
                    WHEN LOWER(""Provider"") = 'sagemaker' THEN 14
                    WHEN LOWER(""Provider"") = 'openrouter' THEN 15
                    WHEN LOWER(""Provider"") = 'openaicompatible' THEN 16
                    WHEN LOWER(""Provider"") = 'minimax' THEN 17
                    WHEN LOWER(""Provider"") = 'ultravox' THEN 18
                    WHEN LOWER(""Provider"") = 'elevenlabs' THEN 19
                    WHEN LOWER(""Provider"") = 'googlecloud' THEN 20
                    WHEN LOWER(""Provider"") = 'cerebras' THEN 21
                    WHEN LOWER(""Provider"") = 'awstranscribe' THEN 22
                    ELSE 1 -- Default to OpenAI if unknown
                END
            ");

            // Convert string provider names to ProviderType enum values for AudioCosts
            migrationBuilder.Sql(@"
                UPDATE ""AudioCosts""
                SET ""ProviderTypeTemp"" = CASE 
                    WHEN LOWER(""Provider"") = 'openai' THEN 1
                    WHEN LOWER(""Provider"") = 'anthropic' THEN 2
                    WHEN LOWER(""Provider"") = 'azureopenai' THEN 3
                    WHEN LOWER(""Provider"") = 'gemini' THEN 4
                    WHEN LOWER(""Provider"") = 'vertexai' THEN 5
                    WHEN LOWER(""Provider"") = 'cohere' THEN 6
                    WHEN LOWER(""Provider"") = 'mistral' THEN 7
                    WHEN LOWER(""Provider"") = 'groq' THEN 8
                    WHEN LOWER(""Provider"") = 'ollama' THEN 9
                    WHEN LOWER(""Provider"") = 'replicate' THEN 10
                    WHEN LOWER(""Provider"") = 'fireworks' THEN 11
                    WHEN LOWER(""Provider"") = 'bedrock' THEN 12
                    WHEN LOWER(""Provider"") = 'huggingface' THEN 13
                    WHEN LOWER(""Provider"") = 'sagemaker' THEN 14
                    WHEN LOWER(""Provider"") = 'openrouter' THEN 15
                    WHEN LOWER(""Provider"") = 'openaicompatible' THEN 16
                    WHEN LOWER(""Provider"") = 'minimax' THEN 17
                    WHEN LOWER(""Provider"") = 'ultravox' THEN 18
                    WHEN LOWER(""Provider"") = 'elevenlabs' THEN 19
                    WHEN LOWER(""Provider"") = 'googlecloud' THEN 20
                    WHEN LOWER(""Provider"") = 'cerebras' THEN 21
                    WHEN LOWER(""Provider"") = 'awstranscribe' THEN 22
                    ELSE 1 -- Default to OpenAI if unknown
                END
            ");

            // Drop the old Provider column
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "AudioUsageLogs");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "AudioCosts");

            // Rename temporary column to Provider
            migrationBuilder.RenameColumn(
                name: "ProviderTypeTemp",
                table: "AudioUsageLogs",
                newName: "Provider");

            migrationBuilder.RenameColumn(
                name: "ProviderTypeTemp",
                table: "AudioCosts",
                newName: "Provider");

            // Make the new Provider columns non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "Provider",
                table: "AudioUsageLogs",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Provider",
                table: "AudioCosts",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add temporary string columns
            migrationBuilder.AddColumn<string>(
                name: "ProviderStringTemp",
                table: "AudioUsageLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStringTemp",
                table: "AudioCosts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Convert ProviderType enum values back to string names for AudioUsageLogs
            migrationBuilder.Sql(@"
                UPDATE ""AudioUsageLogs""
                SET ""ProviderStringTemp"" = CASE 
                    WHEN ""Provider"" = 1 THEN 'openai'
                    WHEN ""Provider"" = 2 THEN 'anthropic'
                    WHEN ""Provider"" = 3 THEN 'azureopenai'
                    WHEN ""Provider"" = 4 THEN 'gemini'
                    WHEN ""Provider"" = 5 THEN 'vertexai'
                    WHEN ""Provider"" = 6 THEN 'cohere'
                    WHEN ""Provider"" = 7 THEN 'mistral'
                    WHEN ""Provider"" = 8 THEN 'groq'
                    WHEN ""Provider"" = 9 THEN 'ollama'
                    WHEN ""Provider"" = 10 THEN 'replicate'
                    WHEN ""Provider"" = 11 THEN 'fireworks'
                    WHEN ""Provider"" = 12 THEN 'bedrock'
                    WHEN ""Provider"" = 13 THEN 'huggingface'
                    WHEN ""Provider"" = 14 THEN 'sagemaker'
                    WHEN ""Provider"" = 15 THEN 'openrouter'
                    WHEN ""Provider"" = 16 THEN 'openaicompatible'
                    WHEN ""Provider"" = 17 THEN 'minimax'
                    WHEN ""Provider"" = 18 THEN 'ultravox'
                    WHEN ""Provider"" = 19 THEN 'elevenlabs'
                    WHEN ""Provider"" = 20 THEN 'googlecloud'
                    WHEN ""Provider"" = 21 THEN 'cerebras'
                    WHEN ""Provider"" = 22 THEN 'awstranscribe'
                    ELSE 'openai' -- Default to openai
                END
            ");

            // Convert ProviderType enum values back to string names for AudioCosts
            migrationBuilder.Sql(@"
                UPDATE ""AudioCosts""
                SET ""ProviderStringTemp"" = CASE 
                    WHEN ""Provider"" = 1 THEN 'openai'
                    WHEN ""Provider"" = 2 THEN 'anthropic'
                    WHEN ""Provider"" = 3 THEN 'azureopenai'
                    WHEN ""Provider"" = 4 THEN 'gemini'
                    WHEN ""Provider"" = 5 THEN 'vertexai'
                    WHEN ""Provider"" = 6 THEN 'cohere'
                    WHEN ""Provider"" = 7 THEN 'mistral'
                    WHEN ""Provider"" = 8 THEN 'groq'
                    WHEN ""Provider"" = 9 THEN 'ollama'
                    WHEN ""Provider"" = 10 THEN 'replicate'
                    WHEN ""Provider"" = 11 THEN 'fireworks'
                    WHEN ""Provider"" = 12 THEN 'bedrock'
                    WHEN ""Provider"" = 13 THEN 'huggingface'
                    WHEN ""Provider"" = 14 THEN 'sagemaker'
                    WHEN ""Provider"" = 15 THEN 'openrouter'
                    WHEN ""Provider"" = 16 THEN 'openaicompatible'
                    WHEN ""Provider"" = 17 THEN 'minimax'
                    WHEN ""Provider"" = 18 THEN 'ultravox'
                    WHEN ""Provider"" = 19 THEN 'elevenlabs'
                    WHEN ""Provider"" = 20 THEN 'googlecloud'
                    WHEN ""Provider"" = 21 THEN 'cerebras'
                    WHEN ""Provider"" = 22 THEN 'awstranscribe'
                    ELSE 'openai' -- Default to openai
                END
            ");

            // Drop the integer Provider column
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "AudioUsageLogs");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "AudioCosts");

            // Rename temporary column to Provider
            migrationBuilder.RenameColumn(
                name: "ProviderStringTemp",
                table: "AudioUsageLogs",
                newName: "Provider");

            migrationBuilder.RenameColumn(
                name: "ProviderStringTemp",
                table: "AudioCosts",
                newName: "Provider");

            // Make the Provider columns non-nullable with default
            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "AudioUsageLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "openai",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "AudioCosts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "openai",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}

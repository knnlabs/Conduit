using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class ConvertProviderToEnumType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new integer column for ProviderType enum
            migrationBuilder.AddColumn<int>(
                name: "ProviderTypeTemp",
                table: "ModelIdentifiers",
                type: "integer",
                nullable: true);

            // Migrate existing string data to integer enum values
            migrationBuilder.Sql(@"
                UPDATE ""ModelIdentifiers""
                SET ""ProviderTypeTemp"" = 
                    CASE 
                        WHEN LOWER(""Provider"") = 'openai' THEN 1
                        WHEN LOWER(""Provider"") = 'groq' THEN 2
                        WHEN LOWER(""Provider"") = 'replicate' THEN 3
                        WHEN LOWER(""Provider"") = 'fireworks' THEN 4
                        WHEN LOWER(""Provider"") = 'openaicompatible' THEN 5
                        WHEN LOWER(""Provider"") = 'minimax' THEN 6
                        WHEN LOWER(""Provider"") = 'ultravox' THEN 7
                        WHEN LOWER(""Provider"") = 'elevenlabs' THEN 8
                        WHEN LOWER(""Provider"") = 'cerebras' THEN 9
                        WHEN LOWER(""Provider"") = 'sambanova' THEN 10
                        WHEN LOWER(""Provider"") = 'deepinfra' THEN 11
                        ELSE NULL
                    END
                WHERE ""Provider"" IS NOT NULL
            ");

            // Drop the old string column
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "ModelIdentifiers");

            // Rename the temporary column to Provider
            migrationBuilder.RenameColumn(
                name: "ProviderTypeTemp",
                table: "ModelIdentifiers",
                newName: "Provider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back the string column
            migrationBuilder.AddColumn<string>(
                name: "ProviderTemp",
                table: "ModelIdentifiers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Convert integer enum values back to strings
            migrationBuilder.Sql(@"
                UPDATE ""ModelIdentifiers""
                SET ""ProviderTemp"" = 
                    CASE 
                        WHEN ""Provider"" = 1 THEN 'openai'
                        WHEN ""Provider"" = 2 THEN 'groq'
                        WHEN ""Provider"" = 3 THEN 'replicate'
                        WHEN ""Provider"" = 4 THEN 'fireworks'
                        WHEN ""Provider"" = 5 THEN 'openaicompatible'
                        WHEN ""Provider"" = 6 THEN 'minimax'
                        WHEN ""Provider"" = 7 THEN 'ultravox'
                        WHEN ""Provider"" = 8 THEN 'elevenlabs'
                        WHEN ""Provider"" = 9 THEN 'cerebras'
                        WHEN ""Provider"" = 10 THEN 'sambanova'
                        WHEN ""Provider"" = 11 THEN 'deepinfra'
                        ELSE NULL
                    END
                WHERE ""Provider"" IS NOT NULL
            ");

            // Drop the integer column
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "ModelIdentifiers");

            // Rename back to Provider
            migrationBuilder.RenameColumn(
                name: "ProviderTemp",
                table: "ModelIdentifiers",
                newName: "Provider");
        }
    }
}
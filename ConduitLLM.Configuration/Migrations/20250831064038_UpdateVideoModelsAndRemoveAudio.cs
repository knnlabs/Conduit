using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVideoModelsAndRemoveAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update video model capabilities
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsVideoGeneration"" = true
                WHERE ""Name"" IN (
                    'veo-2',
                    'veo-3',
                    'veo-3-fast',
                    'video-01',
                    'ray-2-720p',
                    'seedance-1-lite',
                    'seedance-1-pro',
                    'pixverse-v4.5'
                )
            ");

            // Delete Whisper audio models and their related data
            // First delete ModelProviderMappings that reference these models
            migrationBuilder.Sql(@"
                DELETE FROM ""ModelProviderMappings"" 
                WHERE ""ModelProviderTypeAssociationId"" IN (
                    SELECT mpta.""Id"" 
                    FROM ""ModelIdentifiers"" mpta
                    INNER JOIN ""Models"" m ON mpta.""ModelId"" = m.""Id""
                    WHERE m.""Name"" IN ('whisper-large-v3', 'whisper-large-v3-turbo')
                )
            ");

            // Delete ModelIdentifiers (ModelProviderTypeAssociations)
            migrationBuilder.Sql(@"
                DELETE FROM ""ModelIdentifiers"" 
                WHERE ""ModelId"" IN (
                    SELECT ""Id"" FROM ""Models"" 
                    WHERE ""Name"" IN ('whisper-large-v3', 'whisper-large-v3-turbo')
                )
            ");

            // Finally delete the Models themselves
            migrationBuilder.Sql(@"
                DELETE FROM ""Models"" 
                WHERE ""Name"" IN ('whisper-large-v3', 'whisper-large-v3-turbo')
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert video model capabilities
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsVideoGeneration"" = false
                WHERE ""Name"" IN (
                    'veo-2',
                    'veo-3',
                    'veo-3-fast',
                    'video-01',
                    'ray-2-720p',
                    'seedance-1-lite',
                    'seedance-1-pro',
                    'pixverse-v4.5'
                )
            ");

            // Note: Cannot restore deleted Whisper models without complete data
            // Would need to re-run the original seed migration to restore them
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RestoreModelCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Based on original ModelCapabilities mappings from SeedModelData migration
            
            // Standard LLM models (was ModelCapabilitiesId 1): Chat, FunctionCalling, Streaming
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsFunctionCalling"" = true,
                    ""SupportsStreaming"" = true
                WHERE ""Name"" IN (
                    'llama-3.1-8b-instant',
                    'llama-3.3-70b-versatile',
                    'gpt-oss-120b',
                    'gpt-oss-20b',
                    'glm-4p5',
                    'GLM-4.5V',
                    'kimi-k2-instruct',
                    'Kimi-K2-Instruct'
                )
            ");

            // LLM with Vision (was ModelCapabilitiesId 2): Chat, FunctionCalling, Streaming, Vision
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsFunctionCalling"" = true,
                    ""SupportsStreaming"" = true,
                    ""SupportsVision"" = true
                WHERE ""Name"" = 'llama-4-scout'
            ");

            // Extended context models (was ModelCapabilitiesId 3): Chat, FunctionCalling, Streaming
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsFunctionCalling"" = true,
                    ""SupportsStreaming"" = true
                WHERE ""Name"" = 'qwen3-coder-480b-a35b-instruct'
            ");

            // Massive context with vision (was ModelCapabilitiesId 4): Chat, FunctionCalling, Streaming, Vision
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsFunctionCalling"" = true,
                    ""SupportsStreaming"" = true,
                    ""SupportsVision"" = true
                WHERE ""Name"" = 'Llama-4-Maverick'
            ");

            // Whisper models (was ModelCapabilitiesId 5): AudioTranscription (not in current schema)
            // Note: AudioTranscription was removed, these are speech-to-text models
            
            // Image generation models (was ModelCapabilitiesId 6): ImageGeneration
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsImageGeneration"" = true
                WHERE ""Name"" IN (
                    'flux-kontext-pro',
                    'SSD-1B'
                )
            ");

            // Video generation models (was ModelCapabilitiesId 7): VideoGeneration
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsVideoGeneration"" = true
                WHERE ""Name"" IN (
                    'SeeDance-T2V',
                    'Wan2.1-T2V-14B',
                    'hunyuan-video',
                    'hailuo-02'
                )
            ");

            // Content moderation with vision (was ModelCapabilitiesId 8): Chat, Vision, Streaming
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsVision"" = true,
                    ""SupportsStreaming"" = true
                WHERE ""Name"" = 'llama-guard-4-12b'
            ");

            // Small/Medium/Large context models (was ModelCapabilitiesId 9,10,11): Chat, FunctionCalling, Streaming
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsFunctionCalling"" = true,
                    ""SupportsStreaming"" = true
                WHERE ""Name"" IN (
                    'llama-3.1-8b',
                    'llama-3.3-70b'
                )
            ");

            // Reasoning models (was ModelCapabilitiesId 12): Chat, Streaming (no function calling)
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsStreaming"" = true
                WHERE ""Name"" IN (
                    'openai-oss',
                    'qwen-3-32b'
                )
            ");

            // Multimodal with vision (was ModelCapabilitiesId 13): Chat, FunctionCalling, Streaming, Vision
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsFunctionCalling"" = true,
                    ""SupportsStreaming"" = true,
                    ""SupportsVision"" = true
                WHERE ""Name"" = 'GLM-4.5V'
            ");

            // Legacy models (was ModelCapabilitiesId 14): Chat, Streaming (no function calling)
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = true,
                    ""SupportsStreaming"" = true
                WHERE ""Name"" = 'chronos-hermes-13b-v2'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reset all capabilities to false
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsChat"" = false,
                    ""SupportsFunctionCalling"" = false,
                    ""SupportsStreaming"" = false,
                    ""SupportsVision"" = false,
                    ""SupportsImageGeneration"" = false,
                    ""SupportsVideoGeneration"" = false,
                    ""SupportsEmbeddings"" = false
            ");
        }
    }
}

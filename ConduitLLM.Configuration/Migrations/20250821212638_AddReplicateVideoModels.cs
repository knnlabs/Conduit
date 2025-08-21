using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddReplicateVideoModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Insert new ModelAuthors for Replicate providers
            migrationBuilder.InsertData(
                table: "ModelAuthors",
                columns: new[] { "Id", "Name", "Description", "WebsiteUrl" },
                values: new object[,]
                {
                    { 15, "Google", "Google AI - Video generation models", "https://ai.google" },
                    { 16, "Luma", "Luma AI - Ray video generation series", "https://lumalabs.ai" },
                    { 17, "Pixverse", "Pixverse - AI video generation", "https://pixverse.ai" },
                    { 18, "KwaiVGI", "Kwai Video Generation Intelligence - Kling series", "https://kling.kuaishou.com" },
                    { 19, "Tencent", "Tencent - Hunyuan video models", "https://cloud.tencent.com" },
                    { 20, "LeonardoAI", "Leonardo AI - Motion video generation", "https://leonardo.ai" },
                    { 21, "Lightricks", "Lightricks - LTX video generation", "https://lightricks.com" },
                    { 22, "GenmoAI", "Genmo AI - Mochi video models", "https://genmo.ai" },
                    { 23, "Fofr", "Fofr - Video processing models", null },
                    { 24, "Replicate Community", "Community contributed models", "https://replicate.com" },
                    { 25, "MiniMax", "MiniMax - AI video and model generation", "https://minimax.chat" },
                    { 26, "WaveSpeedAI", "WaveSpeed AI - Accelerated AI inference", "https://wavespeed.ai" },
                    { 27, "Wan-Video", "Wan Video - AI video generation models", null }
                });

            // Step 2: Insert new ModelSeries for Replicate models (skip existing ones)
            migrationBuilder.InsertData(
                table: "ModelSeries",
                columns: new[] { "Id", "AuthorId", "Name", "Description", "TokenizerType", "Parameters" },
                values: new object[,]
                {
                    { 15, 15, "Veo", "Google's Veo video generation series", 36, @"{""prompt"":{""type"":""text"",""label"":""Prompt"",""required"":true},""resolution"":{""type"":""select"",""options"":[{""value"":""720p"",""label"":""720p""},{""value"":""1080p"",""label"":""1080p""}],""default"":""720p"",""label"":""Resolution""},""seed"":{""type"":""number"",""label"":""Seed"",""min"":0,""max"":999999}}" },
                    // SeeDance series already exists as ID 13 with AuthorId 7 (ByteDance)
                    { 16, 25, "Hailuo", "MiniMax's Hailuo video series", 36, "{}" },
                    { 17, 25, "Video-01", "MiniMax's Video-01 series", 36, "{}" },
                    { 18, 27, "Wan 2.2", "Wan 2.2 video generation models", 36, "{}" },
                    { 19, 26, "Wan 2.1", "WaveSpeed's Wan 2.1 video generation models", 36, "{}" },
                    { 20, 16, "Ray", "Luma's Ray video generation series", 36, "{}" },
                    { 21, 17, "Pixverse", "Pixverse video generation series", 36, "{}" },
                    { 22, 18, "Kling", "Kwai's Kling video generation series", 36, "{}" },
                    { 23, 19, "Hunyuan", "Tencent's Hunyuan video models", 36, "{}" },
                    { 24, 20, "Motion", "Leonardo's Motion video generation", 36, "{}" },
                    { 25, 21, "LTX", "Lightricks' LTX video generation", 36, "{}" },
                    { 26, 22, "Mochi", "Genmo's Mochi video models", 36, "{}" }
                });

            // Step 3: Insert new ModelCapabilities for video generation
            migrationBuilder.InsertData(
                table: "ModelCapabilities",
                columns: new[] { "Id", "MaxTokens", "MinTokens", "SupportsVision", "SupportsAudioTranscription", 
                                "SupportsTextToSpeech", "SupportsRealtimeAudio", "SupportsImageGeneration", 
                                "SupportsVideoGeneration", "SupportsEmbeddings", "SupportsChat", 
                                "SupportsFunctionCalling", "SupportsStreaming", "TokenizerType", 
                                "SupportedVoices", "SupportedLanguages", "SupportedFormats" },
                values: new object[,]
                {
                    // Standard Video Generation (Text-to-Video)
                    { 15, 77, 1, false, false, false, false, false, true, false, false, false, false, 36, null, null, null },
                    
                    // Image-to-Video Generation
                    { 16, 77, 1, true, false, false, false, false, true, false, false, false, false, 36, null, null, null },
                    
                    // Advanced Video Generation with Audio
                    { 17, 77, 1, false, false, false, false, false, true, false, false, false, false, 36, null, null, "[\"mp4\",\"webm\"]" }
                });

            var utcNow = new System.DateTime(2025, 8, 21, 0, 0, 0, System.DateTimeKind.Utc);

            // Step 4: Insert Replicate Video Models
            migrationBuilder.InsertData(
                table: "Models",
                columns: new[] { "Id", "Name", "Version", "Description", "ModelCardUrl", 
                               "ModelSeriesId", "ModelCapabilitiesId", "IsActive", "CreatedAt", "UpdatedAt", "ApiParameters" },
                values: new object[,]
                {
                    // Google Veo models
                    { 24, "veo-3", "3", "Google's flagship Veo 3 text to video model with audio", null, 15, 17, true, utcNow, utcNow, 
                        @"[""prompt"",""image"",""resolution"",""negative_prompt"",""seed""]" },
                    
                    { 25, "veo-3-fast", "3-fast", "Faster and cheaper version of Veo 3 with audio", null, 15, 17, true, utcNow, utcNow,
                        @"[""prompt"",""image"",""resolution"",""negative_prompt"",""seed""]" },
                    
                    { 26, "veo-2", "2", "State of the art video generation with complex instruction following", null, 15, 15, true, utcNow, utcNow,
                        @"[""prompt"",""duration"",""aspect_ratio"",""seed"",""image""]" },

                    // ByteDance SeeDance models (using existing SeeDance series ID 13)
                    { 27, "seedance-1-pro", "1-pro", "Text and image to video, 5s or 10s, up to 1080p", null, 13, 16, true, utcNow, utcNow,
                        @"[""prompt"",""image"",""duration"",""resolution"",""aspect_ratio"",""camera_fixed"",""seed"",""fps""]" },
                    
                    { 28, "seedance-1-lite", "1-lite", "Video generation with text and image to video support", null, 13, 16, true, utcNow, utcNow,
                        @"[""prompt"",""image"",""duration"",""resolution"",""aspect_ratio"",""camera_fixed"",""seed"",""fps"",""last_frame_image""]" },

                    // MiniMax models
                    { 29, "hailuo-02", "02", "Text and image to video, 6s or 10s videos", null, 16, 16, true, utcNow, utcNow,
                        @"[""prompt"",""duration"",""resolution"",""prompt_optimizer"",""first_frame_image""]" },
                    
                    { 30, "video-01", "01", "Generate 6s videos with prompts or images", null, 17, 16, true, utcNow, utcNow,
                        @"[""prompt"",""prompt_optimizer"",""first_frame_image"",""subject_reference""]" },

                    // Luma Ray models
                    { 31, "ray-2-720p", "2-720p", "Generate 5s and 9s 720p videos", null, 20, 15, true, utcNow, utcNow,
                        @"[""prompt"",""duration"",""aspect_ratio"",""start_image"",""end_image"",""loop"",""concepts""]" },

                    // Pixverse models
                    { 32, "pixverse-v4.5", "4.5", "Quickly make 5s or 8s videos with enhanced motion", null, 21, 16, true, utcNow, utcNow,
                        @"[""prompt"",""quality"",""duration"",""aspect_ratio"",""motion_mode"",""style"",""effect"",""seed"",""negative_prompt"",""image"",""last_frame_image"",""sound_effect_switch""]" },
                    
                    // Kling models  
                    { 33, "kling-v2.1", "2.1", "Generate 5s and 10s videos from a starting image", null, 22, 16, true, utcNow, utcNow,
                        @"[""prompt"",""start_image"",""mode"",""duration"",""negative_prompt""]" },

                    // Tencent Hunyuan
                    { 34, "hunyuan-video", "1.0", "State-of-the-art text-to-video generation model", null, 23, 15, true, utcNow, utcNow,
                        @"[""prompt"",""width"",""height"",""video_length"",""infer_steps"",""embedded_guidance_scale"",""fps"",""seed""]" }
                });

            // Step 5: Insert ModelIdentifiers for Replicate
            migrationBuilder.InsertData(
                table: "ModelIdentifiers",
                columns: new[] { "Id", "ModelId", "Identifier", "Provider", "IsPrimary", "Metadata" },
                values: new object[,]
                {
                    // Google models
                    { 188, 24, "google/veo-3", "replicate", true, null },
                    { 189, 25, "google/veo-3-fast", "replicate", true, null },
                    { 190, 26, "google/veo-2", "replicate", true, null },
                    
                    // ByteDance models
                    { 191, 27, "bytedance/seedance-1-pro", "replicate", true, null },
                    { 192, 28, "bytedance/seedance-1-lite", "replicate", true, null },
                    
                    // MiniMax models
                    { 193, 29, "minimax/hailuo-02", "replicate", true, null },
                    { 194, 30, "minimax/video-01", "replicate", true, null },
                    
                    // Luma models
                    { 195, 31, "luma/ray-2-720p", "replicate", true, null },
                    
                    // Pixverse models
                    { 196, 32, "pixverse/pixverse-v4.5", "replicate", true, null },
                    
                    // Kling models
                    { 197, 33, "kwaivgi/kling-v2.1", "replicate", true, null },
                    
                    // Tencent models
                    { 198, 34, "tencent/hunyuan-video", "replicate", true, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove ModelIdentifiers
            migrationBuilder.DeleteData(
                table: "ModelIdentifiers",
                keyColumn: "Id",
                keyValues: new object[] { 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198 });

            // Remove Models
            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValues: new object[] { 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 });

            // Remove ModelCapabilities
            migrationBuilder.DeleteData(
                table: "ModelCapabilities",
                keyColumn: "Id",
                keyValues: new object[] { 15, 16, 17 });

            // Remove ModelSeries (skip ID 13 which already existed)
            migrationBuilder.DeleteData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValues: new object[] { 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26 });

            // Remove ModelAuthors
            migrationBuilder.DeleteData(
                table: "ModelAuthors",
                keyColumn: "Id",
                keyValues: new object[] { 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27 });
        }
    }
}

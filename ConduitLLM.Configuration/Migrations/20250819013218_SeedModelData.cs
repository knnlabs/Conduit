using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class SeedModelData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Insert ModelAuthors
            migrationBuilder.InsertData(
                table: "ModelAuthors",
                columns: new[] { "Id", "Name", "Description", "WebsiteUrl" },
                values: new object[,]
                {
                    { 1, "Meta", "Meta AI (formerly Facebook AI)", "https://ai.meta.com" },
                    { 2, "OpenAI", "OpenAI - creators of GPT models", "https://openai.com" },
                    { 3, "Groq", "Groq - high-performance inference", "https://groq.com" },
                    { 4, "Fireworks", "Fireworks AI - fast inference platform", "https://fireworks.ai" },
                    { 5, "Cerebras", "Cerebras - ultra-fast AI compute", "https://cerebras.net" },
                    { 6, "DeepInfra", "DeepInfra - serverless AI inference", "https://deepinfra.com" },
                    { 7, "ByteDance", "ByteDance - creators of SeeDance and other models", "https://bytedance.com" },
                    { 8, "Wan-AI", "Wan AI - video generation models", null },
                    { 9, "Moonshot", "Moonshot AI - creators of Kimi models", "https://moonshotai.com" },
                    { 10, "ZAI", "ZAI Organization", null },
                    { 11, "Zhipu", "Zhipu AI - creators of GLM models", "https://zhipuai.cn" },
                    { 12, "Qwen", "Alibaba Qwen Team", "https://qwenlm.github.io" }
                });

            // Step 2: Insert ModelCapabilities (unique combinations)
            migrationBuilder.InsertData(
                table: "ModelCapabilities",
                columns: new[] { "Id", "MaxTokens", "MinTokens", "SupportsVision", "SupportsAudioTranscription", 
                                "SupportsTextToSpeech", "SupportsRealtimeAudio", "SupportsImageGeneration", 
                                "SupportsVideoGeneration", "SupportsEmbeddings", "SupportsChat", 
                                "SupportsFunctionCalling", "SupportsStreaming", "TokenizerType", 
                                "SupportedVoices", "SupportedLanguages", "SupportedFormats" },
                values: new object[,]
                {
                    // LLM Standard (131K context, chat, streaming, function calling)
                    { 1, 131072, 1, false, false, false, false, false, false, false, true, true, true, 3, null, null, null },
                    
                    // LLM Standard with Vision
                    { 2, 131072, 1, true, false, false, false, false, false, false, true, true, true, 3, null, null, null },
                    
                    // LLM Extended (262K context)
                    { 3, 262144, 1, false, false, false, false, false, false, false, true, true, true, 36, null, null, null },
                    
                    // LLM Massive (1M+ context)
                    { 4, 1048576, 1, true, false, false, false, false, false, false, true, true, true, 3, null, null, null },
                    
                    // Speech-to-Text (Whisper)
                    { 5, 1024, 1, false, true, false, false, false, false, false, false, false, false, 38, null, "[\"en\",\"es\",\"fr\",\"de\",\"ja\",\"zh\"]", "[\"json\",\"text\",\"srt\",\"vtt\"]" },
                    
                    // Image Generation
                    { 6, 77, 1, false, false, false, false, true, false, false, false, false, false, 36, null, null, null },
                    
                    // Video Generation
                    { 7, 77, 1, false, false, false, false, false, true, false, false, false, false, 36, null, null, null },
                    
                    // Content Moderation with Vision
                    { 8, 1024, 1, true, false, false, false, false, false, false, true, false, true, 21, null, null, null },
                    
                    // LLM Small Context (8K)
                    { 9, 8192, 1, false, false, false, false, false, false, false, true, true, true, 21, null, null, null },
                    
                    // LLM Medium Context (32K)
                    { 10, 32768, 1, false, false, false, false, false, false, false, true, true, true, 21, null, null, null },
                    
                    // LLM Large Context (65K)
                    { 11, 65536, 1, false, false, false, false, false, false, false, true, true, true, 21, null, null, null },
                    
                    // LLM Reasoning Context (64K)
                    { 12, 65536, 1, false, false, false, false, false, false, false, true, false, true, 36, null, null, null },
                    
                    // Multimodal Vision (65K)
                    { 13, 65536, 1, true, false, false, false, false, false, false, true, true, true, 36, null, null, null },
                    
                    // LLM Legacy (4K)
                    { 14, 4096, 1, false, false, false, false, false, false, false, true, false, true, 24, null, null, null }
                });

            // Step 3: Insert ModelSeries
            migrationBuilder.InsertData(
                table: "ModelSeries",
                columns: new[] { "Id", "AuthorId", "Name", "Description", "TokenizerType", "Parameters" },
                values: new object[,]
                {
                    { 1, 1, "LLaMA 3.1", "Meta's LLaMA 3.1 series", 21, "{}" },
                    { 2, 1, "LLaMA 3.3", "Meta's LLaMA 3.3 series", 21, "{}" },
                    { 3, 1, "LLaMA 4", "Meta's LLaMA 4 series including Scout and Maverick", 21, "{}" },
                    { 4, 1, "LLaMA Guard", "Meta's content moderation models", 21, "{}" },
                    { 5, 2, "GPT-OSS", "OpenAI's open-source GPT models", 3, "{\"reasoning_effort\":{\"type\":\"select\",\"options\":[{\"value\":\"low\",\"label\":\"Low\"},{\"value\":\"medium\",\"label\":\"Medium\"},{\"value\":\"high\",\"label\":\"High\"}],\"default\":\"medium\",\"label\":\"Reasoning Effort\"}}" },
                    { 6, 3, "Whisper", "OpenAI's Whisper speech recognition", 38, "{}" },
                    { 7, 4, "Flux", "Fireworks' image generation models", 36, "{\"guidance_scale\":{\"type\":\"slider\",\"min\":1,\"max\":20,\"step\":0.5,\"default\":7.5,\"label\":\"Guidance Scale\"},\"num_inference_steps\":{\"type\":\"slider\",\"min\":20,\"max\":50,\"step\":1,\"default\":30,\"label\":\"Inference Steps\"}}" },
                    { 8, 4, "SSD", "Stable Diffusion models", 36, "{\"negative_prompt\":{\"type\":\"text\",\"label\":\"Negative Prompt\"},\"scheduler\":{\"type\":\"select\",\"options\":[{\"value\":\"DDIM\",\"label\":\"DDIM\"},{\"value\":\"DPM\",\"label\":\"DPM\"}],\"default\":\"DDIM\",\"label\":\"Scheduler\"}}" },
                    { 9, 12, "Qwen 3", "Alibaba's Qwen 3 series", 36, "{}" },
                    { 10, 11, "GLM 4", "Zhipu's GLM 4 series", 35, "{}" },
                    { 11, 9, "Kimi", "Moonshot's Kimi series", 36, "{}" },
                    { 12, 4, "Chronos", "Fireworks' Chronos series", 24, "{}" },
                    { 13, 7, "SeeDance", "ByteDance's video generation", 36, "{}" },
                    { 14, 8, "Wan", "Wan AI's video generation", 36, "{\"guidance_scale\":{\"type\":\"slider\",\"min\":1,\"max\":15,\"step\":0.5,\"default\":7,\"label\":\"Guidance Scale\"}}" }
                });

            // Step 4: Insert Models
            migrationBuilder.InsertData(
                table: "Models",
                columns: new[] { "Id", "Name", "Version", "Description", "ModelCardUrl", "ModelType", 
                               "ModelSeriesId", "ModelCapabilitiesId", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    // Groq Models
                    { 1, "llama-3.1-8b-instant", "3.1", "Fast 8B parameter LLaMA model", null, 0, 1, 1, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "llama-3.3-70b-versatile", "3.3", "Versatile 70B parameter LLaMA model", null, 0, 2, 1, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "llama-guard-4-12b", "4", "Content moderation model with vision", null, 0, 4, 8, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "whisper-large-v3", "v3", "Large Whisper speech-to-text model", null, 2, 6, 5, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "whisper-large-v3-turbo", "v3-turbo", "Turbo variant of Whisper large", null, 2, 6, 5, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "gpt-oss-120b", "120b", "120B parameter GPT-OSS model", null, 0, 5, 1, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, "gpt-oss-20b", "20b", "20B parameter GPT-OSS model", null, 0, 5, 1, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    
                    // Fireworks Models
                    { 8, "flux-kontext-pro", "pro", "Professional image-to-image model", null, 1, 7, 6, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, "qwen3-coder-480b-a35b-instruct", "480b", "Large coding model with extended context", null, 0, 9, 3, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, "glm-4p5", "4.5", "GLM 4.5 model", null, 0, 10, 1, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, "kimi-k2-instruct", "k2", "Kimi K2 instruction model", null, 0, 11, 1, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, "SSD-1B", "1B", "1B parameter Stable Diffusion", null, 1, 8, 6, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, "chronos-hermes-13b-v2", "v2", "13B parameter Chronos model", null, 0, 12, 14, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    
                    // Cerebras Models
                    { 14, "llama-4-scout", "4", "Multimodal LLaMA 4 Scout", null, 0, 3, 2, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, "llama-3.1-8b", "3.1", "8B parameter LLaMA 3.1", null, 0, 1, 10, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, "llama-3.3-70b", "3.3", "70B parameter LLaMA 3.3", null, 0, 2, 11, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, "openai-oss", "120b", "OpenAI OSS reasoning model", null, 0, 5, 12, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, "qwen-3-32b", "3", "32B Qwen reasoning model", null, 0, 9, 12, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    
                    // DeepInfra Models
                    { 19, "Kimi-K2-Instruct", "k2", "Kimi K2 instruction model", null, 0, 11, 1, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, "GLM-4.5V", "4.5V", "Multimodal GLM with vision", null, 0, 10, 13, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, "Llama-4-Maverick", "4", "LLaMA 4 Maverick with 1M context", null, 0, 3, 4, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, "SeeDance-T2V", "1.0", "Text-to-video generation", null, 3, 13, 7, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 23, "Wan2.1-T2V-14B", "2.1", "14B text-to-video model", null, 3, 14, 7, true, new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc) }
                });

            // Step 5: Insert ModelIdentifiers
            migrationBuilder.InsertData(
                table: "ModelIdentifiers",
                columns: new[] { "Id", "ModelId", "Identifier", "Provider", "IsPrimary", "Metadata" },
                values: new object[,]
                {
                    // Primary identifiers from the table
                    { 1, 1, "llama-3.1-8b-instant", "groq", true, null },
                    { 2, 2, "llama-3.3-70b-versatile", "groq", true, null },
                    { 3, 3, "meta-llama/llama-guard-4-12b", "groq", true, null },
                    { 4, 4, "whisper-large-v3", "groq", true, null },
                    { 5, 5, "whisper-large-v3-turbo", "groq", true, null },
                    { 6, 6, "openai/gpt-oss-120b", "groq", true, null },
                    { 7, 7, "openai/gpt-oss-20b", "groq", true, null },
                    
                    // Fireworks identifiers
                    { 8, 7, "gpt-oss-20b", "fireworks", true, null },
                    { 9, 6, "gpt-oss-120b", "fireworks", true, null },
                    { 10, 8, "flux-kontext-pro", "fireworks", true, null },
                    { 11, 9, "qwen3-coder-480b-a35b-instruct", "fireworks", true, null },
                    { 12, 10, "glm-4p5", "fireworks", true, null },
                    { 13, 11, "kimi-k2-instruct", "fireworks", true, null },
                    { 14, 12, "SSD-1B", "fireworks", true, null },
                    { 15, 13, "chronos-hermes-13b-v2", "fireworks", true, null },
                    
                    // Cerebras identifiers
                    { 16, 14, "llama-4-scout", "cerebras", true, null },
                    { 17, 15, "llama-3.1-8b", "cerebras", true, null },
                    { 18, 16, "llama-3.3-70b", "cerebras", true, null },
                    { 19, 17, "openai-oss", "cerebras", true, null },
                    { 20, 17, "gpt-oss-120b", "cerebras", false, null },
                    { 21, 18, "qwen-3-32b", "cerebras", true, null },
                    
                    // DeepInfra identifiers
                    { 22, 19, "moonshotai/Kimi-K2-Instruct", "deepinfra", true, null },
                    { 23, 6, "openai/gpt-oss-120b", "deepinfra", true, null },
                    { 24, 20, "zai-org/GLM-4.5V", "deepinfra", true, null },
                    { 25, 21, "meta-llama/Llama-4-Maverick", "deepinfra", true, null },
                    { 26, 22, "ByteDance/SeeDance-T2V", "deepinfra", true, null },
                    { 27, 23, "Wan-AI/Wan2.1-T2V-14B", "deepinfra", true, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove in reverse order due to foreign key constraints
            
            // Step 1: Remove ModelIdentifiers
            migrationBuilder.DeleteData(
                table: "ModelIdentifiers",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27 });
            
            // Step 2: Remove Models
            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 });
            
            // Step 3: Remove ModelSeries
            migrationBuilder.DeleteData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 });
            
            // Step 4: Remove ModelCapabilities
            migrationBuilder.DeleteData(
                table: "ModelCapabilities",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 });
            
            // Step 5: Remove ModelAuthors
            migrationBuilder.DeleteData(
                table: "ModelAuthors",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });
        }
    }
}

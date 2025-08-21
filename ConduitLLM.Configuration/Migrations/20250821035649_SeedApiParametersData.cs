using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class SeedApiParametersData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update ModelSeries with API parameters
            
            // Whisper series (ID: 6) - Audio transcription parameters
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 6,
                column: "ApiParameters",
                value: "[\"language\",\"prompt\",\"response_format\",\"timestamp_granularities\"]"
            );
            
            // GPT-OSS series (ID: 5) - Reasoning parameters
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 5,
                column: "ApiParameters",
                value: "[\"reasoning_effort\"]"
            );
            
            // LLaMA 3.1 series (ID: 1) - Advanced sampling
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 1,
                column: "ApiParameters",
                value: "[\"min_p\",\"top_k\",\"repetition_penalty\"]"
            );
            
            // LLaMA 3.3 series (ID: 2) - Advanced sampling  
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 2,
                column: "ApiParameters",
                value: "[\"min_p\",\"top_k\",\"repetition_penalty\"]"
            );
            
            // LLaMA 4 series (ID: 3) - Advanced sampling with tool support
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 3,
                column: "ApiParameters",
                value: "[\"min_p\",\"top_k\",\"repetition_penalty\"]"
            );
            
            // Flux series (ID: 7) - Image generation parameters
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 7,
                column: "ApiParameters",
                value: "[\"prompt\",\"image_url\",\"guidance_scale\",\"num_inference_steps\"]"
            );
            
            // SSD series (ID: 8) - Stable Diffusion parameters
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 8,
                column: "ApiParameters",
                value: "[\"negative_prompt\",\"scheduler\",\"num_inference_steps\",\"guidance_scale\"]"
            );
            
            // Qwen 3 series (ID: 9) - Advanced models with reasoning
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 9,
                column: "ApiParameters",
                value: "[\"min_p\",\"top_k\",\"repetition_penalty\"]"
            );
            
            // GLM 4 series (ID: 10) - Advanced sampling
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 10,
                column: "ApiParameters",
                value: "[\"min_p\",\"top_k\",\"repetition_penalty\"]"
            );
            
            // Kimi series (ID: 11) - Advanced sampling
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 11,
                column: "ApiParameters",
                value: "[\"min_p\",\"top_k\",\"repetition_penalty\"]"
            );
            
            // SeeDance series (ID: 13) - Video generation
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 13,
                column: "ApiParameters",
                value: "[\"guidance_scale\",\"seed\",\"negative_prompt\"]"
            );
            
            // Wan series (ID: 14) - Video generation
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 14,
                column: "ApiParameters",
                value: "[\"guidance_scale\",\"seed\",\"negative_prompt\"]"
            );
            
            // Update specific Models with additional parameters
            
            // Kimi-K2-Instruct on DeepInfra (ID: 19) - Add seed and user
            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 19,
                column: "ApiParameters",
                value: "[\"seed\",\"user\"]"
            );
            
            // Llama-4-Maverick on DeepInfra (ID: 21) - Add seed
            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 21,
                column: "ApiParameters",
                value: "[\"seed\"]"
            );
            
            // GPT-OSS models on DeepInfra - Add seed and user
            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 6,
                column: "ApiParameters",
                value: "[\"seed\",\"user\"]"
            );
            
            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 7,
                column: "ApiParameters",
                value: "[\"seed\",\"user\"]"
            );
            
            // Qwen-3-32b (ID: 18) - Add /no_think support
            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 18,
                column: "ApiParameters",
                value: "[\"no_think\"]"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert ModelSeries ApiParameters to null
            var modelSeriesIds = new[] { 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 13, 14 };
            foreach (var id in modelSeriesIds)
            {
                migrationBuilder.UpdateData(
                    table: "ModelSeries",
                    keyColumn: "Id",
                    keyValue: id,
                    column: "ApiParameters",
                    value: null
                );
            }
            
            // Revert Models ApiParameters to null
            var modelIds = new[] { 6, 7, 18, 19, 21 };
            foreach (var id in modelIds)
            {
                migrationBuilder.UpdateData(
                    table: "Models",
                    keyColumn: "Id",
                    keyValue: id,
                    column: "ApiParameters",
                    value: null
                );
            }
        }
    }
}

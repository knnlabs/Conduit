using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoModelParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update Parameters for Veo series (ID: 15) - Google models
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 15,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""5"", ""label"": ""5 seconds""},
                            {""value"": ""6"", ""label"": ""6 seconds""},
                            {""value"": ""7"", ""label"": ""7 seconds""},
                            {""value"": ""8"", ""label"": ""8 seconds""}
                        ],
                        ""default"": ""5"",
                        ""label"": ""Duration""
                    },
                    ""resolution"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""720p"", ""label"": ""720p (1280x720)""}
                        ],
                        ""default"": ""720p"",
                        ""label"": ""Resolution""
                    },
                    ""aspect_ratio"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""16:9"", ""label"": ""16:9 (Widescreen)""},
                            {""value"": ""9:16"", ""label"": ""9:16 (Vertical)""},
                            {""value"": ""1:1"", ""label"": ""1:1 (Square)""}
                        ],
                        ""default"": ""16:9"",
                        ""label"": ""Aspect Ratio""
                    },
                    ""negative_prompt"": {
                        ""type"": ""textarea"",
                        ""label"": ""Negative Prompt"",
                        ""placeholder"": ""Describe what to avoid in the video..."",
                        ""rows"": 2
                    },
                    ""seed"": {
                        ""type"": ""number"",
                        ""label"": ""Seed"",
                        ""min"": 0,
                        ""max"": 999999,
                        ""placeholder"": ""Random seed for reproducibility""
                    },
                    ""enhance_prompt"": {
                        ""type"": ""switch"",
                        ""label"": ""Enhance Prompt"",
                        ""default"": true,
                        ""description"": ""Automatically enhance prompt for better results""
                    }
                }"
            );

            // Update Parameters for Hailuo series (ID: 16) - MiniMax models
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 16,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""6"", ""label"": ""6 seconds""},
                            {""value"": ""10"", ""label"": ""10 seconds""}
                        ],
                        ""default"": ""6"",
                        ""label"": ""Duration""
                    },
                    ""resolution"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""512p"", ""label"": ""512p""},
                            {""value"": ""768p"", ""label"": ""768p""},
                            {""value"": ""1080p"", ""label"": ""1080p (Full HD)""}
                        ],
                        ""default"": ""768p"",
                        ""label"": ""Resolution""
                    },
                    ""prompt_optimizer"": {
                        ""type"": ""switch"",
                        ""label"": ""Prompt Optimizer"",
                        ""default"": true,
                        ""description"": ""Use AI to optimize your prompt""
                    }
                }"
            );

            // Update Parameters for Video-01 series (ID: 17) - MiniMax models
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 17,
                column: "Parameters",
                value: @"{
                    ""prompt_optimizer"": {
                        ""type"": ""switch"",
                        ""label"": ""Prompt Optimizer"",
                        ""default"": true,
                        ""description"": ""Use AI to optimize your prompt""
                    }
                }"
            );

            // Update Parameters for SeeDance series (ID: 13) - ByteDance models
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 13,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": 5, ""label"": ""5 seconds""},
                            {""value"": 10, ""label"": ""10 seconds""}
                        ],
                        ""default"": 5,
                        ""label"": ""Duration""
                    },
                    ""resolution"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""480p"", ""label"": ""480p (SD)""},
                            {""value"": ""720p"", ""label"": ""720p (HD)""},
                            {""value"": ""1080p"", ""label"": ""1080p (Full HD)""}
                        ],
                        ""default"": ""720p"",
                        ""label"": ""Resolution""
                    },
                    ""aspect_ratio"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""16:9"", ""label"": ""16:9 (Widescreen)""},
                            {""value"": ""9:16"", ""label"": ""9:16 (Vertical)""},
                            {""value"": ""4:3"", ""label"": ""4:3 (Standard)""},
                            {""value"": ""1:1"", ""label"": ""1:1 (Square)""},
                            {""value"": ""3:4"", ""label"": ""3:4 (Portrait)""},
                            {""value"": ""21:9"", ""label"": ""21:9 (Ultrawide)""},
                            {""value"": ""9:21"", ""label"": ""9:21 (Ultra Tall)""}
                        ],
                        ""default"": ""16:9"",
                        ""label"": ""Aspect Ratio""
                    },
                    ""fps"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": 24, ""label"": ""24 FPS""}
                        ],
                        ""default"": 24,
                        ""label"": ""Frame Rate""
                    },
                    ""camera_fixed"": {
                        ""type"": ""switch"",
                        ""label"": ""Fixed Camera"",
                        ""default"": false,
                        ""description"": ""Keep camera position fixed""
                    },
                    ""seed"": {
                        ""type"": ""number"",
                        ""label"": ""Seed"",
                        ""min"": 0,
                        ""max"": 999999,
                        ""placeholder"": ""Random seed for reproducibility""
                    }
                }"
            );

            // Update Parameters for Ray series (ID: 20) - Luma models
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 20,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": 5, ""label"": ""5 seconds""},
                            {""value"": 9, ""label"": ""9 seconds""}
                        ],
                        ""default"": 5,
                        ""label"": ""Duration""
                    },
                    ""aspect_ratio"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""1:1"", ""label"": ""1:1 (Square)""},
                            {""value"": ""3:4"", ""label"": ""3:4 (Portrait)""},
                            {""value"": ""4:3"", ""label"": ""4:3 (Standard)""},
                            {""value"": ""9:16"", ""label"": ""9:16 (Vertical)""},
                            {""value"": ""16:9"", ""label"": ""16:9 (Widescreen)""},
                            {""value"": ""9:21"", ""label"": ""9:21 (Ultra Tall)""},
                            {""value"": ""21:9"", ""label"": ""21:9 (Ultrawide)""}
                        ],
                        ""default"": ""16:9"",
                        ""label"": ""Aspect Ratio""
                    },
                    ""loop"": {
                        ""type"": ""switch"",
                        ""label"": ""Loop Video"",
                        ""default"": false,
                        ""description"": ""Create a seamless loop""
                    }
                }"
            );

            // Update Parameters for Pixverse series (ID: 21) - Pixverse models
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 21,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": 5, ""label"": ""5 seconds""},
                            {""value"": 8, ""label"": ""8 seconds""}
                        ],
                        ""default"": 5,
                        ""label"": ""Duration""
                    },
                    ""quality"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""360p"", ""label"": ""360p (Low)""},
                            {""value"": ""540p"", ""label"": ""540p (Medium)""},
                            {""value"": ""720p"", ""label"": ""720p (HD)""},
                            {""value"": ""1080p"", ""label"": ""1080p (Full HD)""}
                        ],
                        ""default"": ""540p"",
                        ""label"": ""Quality""
                    },
                    ""aspect_ratio"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""16:9"", ""label"": ""16:9 (Widescreen)""},
                            {""value"": ""9:16"", ""label"": ""9:16 (Vertical)""},
                            {""value"": ""1:1"", ""label"": ""1:1 (Square)""}
                        ],
                        ""default"": ""16:9"",
                        ""label"": ""Aspect Ratio""
                    },
                    ""motion_mode"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""normal"", ""label"": ""Normal""},
                            {""value"": ""smooth"", ""label"": ""Smooth""}
                        ],
                        ""default"": ""normal"",
                        ""label"": ""Motion Mode""
                    },
                    ""style"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""None"", ""label"": ""None""},
                            {""value"": ""anime"", ""label"": ""Anime""},
                            {""value"": ""3d_animation"", ""label"": ""3D Animation""},
                            {""value"": ""clay"", ""label"": ""Clay""},
                            {""value"": ""cyberpunk"", ""label"": ""Cyberpunk""},
                            {""value"": ""comic"", ""label"": ""Comic""}
                        ],
                        ""default"": ""None"",
                        ""label"": ""Style""
                    },
                    ""negative_prompt"": {
                        ""type"": ""textarea"",
                        ""label"": ""Negative Prompt"",
                        ""placeholder"": ""Elements to avoid..."",
                        ""rows"": 2
                    },
                    ""seed"": {
                        ""type"": ""number"",
                        ""label"": ""Seed"",
                        ""min"": 0,
                        ""max"": 999999,
                        ""placeholder"": ""Random seed""
                    }
                }"
            );

            // Update Parameters for Kling series (ID: 22) - KwaiVGI models
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 22,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": 5, ""label"": ""5 seconds""},
                            {""value"": 10, ""label"": ""10 seconds""}
                        ],
                        ""default"": 5,
                        ""label"": ""Duration""
                    },
                    ""aspect_ratio"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""16:9"", ""label"": ""16:9 (Widescreen)""},
                            {""value"": ""9:16"", ""label"": ""9:16 (Vertical)""},
                            {""value"": ""1:1"", ""label"": ""1:1 (Square)""}
                        ],
                        ""default"": ""16:9"",
                        ""label"": ""Aspect Ratio""
                    },
                    ""negative_prompt"": {
                        ""type"": ""textarea"",
                        ""label"": ""Negative Prompt"",
                        ""placeholder"": ""What to avoid..."",
                        ""rows"": 2
                    },
                    ""cfg_scale"": {
                        ""type"": ""slider"",
                        ""min"": 0,
                        ""max"": 1,
                        ""step"": 0.1,
                        ""default"": 0.5,
                        ""label"": ""CFG Scale"",
                        ""description"": ""Classifier-free guidance scale""
                    }
                }"
            );

            // Update Parameters for Hunyuan series (ID: 23) - Tencent models
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 23,
                column: "Parameters",
                value: @"{
                    ""width"": {
                        ""type"": ""number"",
                        ""min"": 16,
                        ""max"": 1280,
                        ""default"": 864,
                        ""label"": ""Width"",
                        ""description"": ""Video width in pixels""
                    },
                    ""height"": {
                        ""type"": ""number"",
                        ""min"": 16,
                        ""max"": 1280,
                        ""default"": 480,
                        ""label"": ""Height"",
                        ""description"": ""Video height in pixels""
                    },
                    ""video_length"": {
                        ""type"": ""slider"",
                        ""min"": 1,
                        ""max"": 200,
                        ""default"": 129,
                        ""label"": ""Video Length"",
                        ""description"": ""Number of frames to generate""
                    },
                    ""fps"": {
                        ""type"": ""number"",
                        ""min"": 1,
                        ""max"": 60,
                        ""default"": 24,
                        ""label"": ""Frame Rate""
                    },
                    ""infer_steps"": {
                        ""type"": ""number"",
                        ""min"": 1,
                        ""max"": 100,
                        ""default"": 50,
                        ""label"": ""Inference Steps"",
                        ""description"": ""Number of denoising steps""
                    },
                    ""embedded_guidance_scale"": {
                        ""type"": ""slider"",
                        ""min"": 1,
                        ""max"": 10,
                        ""step"": 0.5,
                        ""default"": 6,
                        ""label"": ""Guidance Scale""
                    },
                    ""negative_prompt"": {
                        ""type"": ""textarea"",
                        ""label"": ""Negative Prompt"",
                        ""placeholder"": ""Elements to avoid..."",
                        ""rows"": 2
                    },
                    ""seed"": {
                        ""type"": ""number"",
                        ""label"": ""Seed"",
                        ""min"": 0,
                        ""max"": 999999,
                        ""placeholder"": ""Random seed""
                    }
                }"
            );

            // Add new ModelSeries for models that don't have series yet
            // Note: Wan series (ID: 14) already has parameters, keeping them as is

            // Add new series for models that need them (if any)
            // Based on the migration data, we already have the necessary series
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert Veo series parameters
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 15,
                column: "Parameters",
                value: @"{""prompt"":{""type"":""text"",""label"":""Prompt"",""required"":true},""resolution"":{""type"":""select"",""options"":[{""value"":""720p"",""label"":""720p""},{""value"":""1080p"",""label"":""1080p""}],""default"":""720p"",""label"":""Resolution""},""seed"":{""type"":""number"",""label"":""Seed"",""min"":0,""max"":999999}}"
            );

            // Revert other series to empty parameters
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 16,
                column: "Parameters",
                value: "{}"
            );

            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 17,
                column: "Parameters",
                value: "{}"
            );

            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 13,
                column: "Parameters",
                value: "{}"
            );

            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 20,
                column: "Parameters",
                value: "{}"
            );

            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 21,
                column: "Parameters",
                value: "{}"
            );

            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 22,
                column: "Parameters",
                value: "{}"
            );

            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 23,
                column: "Parameters",
                value: "{}"
            );
        }
    }
}
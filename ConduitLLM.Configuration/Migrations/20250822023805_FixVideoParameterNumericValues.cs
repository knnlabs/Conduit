using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class FixVideoParameterNumericValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix SeeDance series (ID: 19) - fps numeric value
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 19,
                column: "Parameters",
                value: @"{
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
                            {""value"": ""24"", ""label"": ""24 FPS""}
                        ],
                        ""default"": ""24"",
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

            // Fix Ray series (ID: 20) - duration numeric values
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 20,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""5"", ""label"": ""5 seconds""},
                            {""value"": ""9"", ""label"": ""9 seconds""}
                        ],
                        ""default"": ""5"",
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

            // Fix Pixverse series (ID: 21) - duration numeric values
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 21,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""5"", ""label"": ""5 seconds""},
                            {""value"": ""8"", ""label"": ""8 seconds""}
                        ],
                        ""default"": ""5"",
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

            // Fix Kling series (ID: 22) - duration numeric values
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 22,
                column: "Parameters",
                value: @"{
                    ""duration"": {
                        ""type"": ""select"",
                        ""options"": [
                            {""value"": ""5"", ""label"": ""5 seconds""},
                            {""value"": ""10"", ""label"": ""10 seconds""}
                        ],
                        ""default"": ""5"",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

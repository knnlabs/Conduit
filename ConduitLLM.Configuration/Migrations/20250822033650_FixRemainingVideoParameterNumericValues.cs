using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class FixRemainingVideoParameterNumericValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix Veo series (ID: 15) - duration numeric values
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
                    }
                }"
            );

            // Fix Hailuo series (ID: 16) - duration numeric values
            migrationBuilder.UpdateData(
                table: "ModelSeries",
                keyColumn: "Id",
                keyValue: 16,
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
                    ""resolution"": {
                        ""type"": ""select"",
                        ""options"": [
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
                            {""value"": ""1:1"", ""label"": ""1:1 (Square)""}
                        ],
                        ""default"": ""16:9"",
                        ""label"": ""Aspect Ratio""
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

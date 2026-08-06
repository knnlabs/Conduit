using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectionalModelModalities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CapabilitiesLastVerifiedAt",
                table: "Models",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CapabilitySource",
                table: "Models",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "InputModalities",
                table: "Models",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputModalities",
                table: "Models",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CapabilitiesLastVerifiedAt",
                table: "ModelIdentifiers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CapabilitySource",
                table: "ModelIdentifiers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputModalities",
                table: "ModelIdentifiers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalCapabilities",
                table: "ModelIdentifiers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputModalities",
                table: "ModelIdentifiers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Models"
                SET
                    "InputModalities" =
                        (CASE WHEN "SupportsChat"
                                   OR "SupportsEmbeddings"
                                   OR "SupportsImageGeneration"
                                   OR "SupportsVideoGeneration"
                                   OR "SupportsTextToSpeech"
                                   OR "SupportsRerank"
                              THEN '["text"]'::jsonb ELSE '[]'::jsonb END)
                        || (CASE WHEN "SupportsVision"
                                 THEN '["image"]'::jsonb ELSE '[]'::jsonb END)
                        || (CASE WHEN "SupportsSpeechToText"
                                 THEN '["audio"]'::jsonb ELSE '[]'::jsonb END),
                    "OutputModalities" =
                        (CASE WHEN "SupportsChat"
                                   OR "SupportsSpeechToText"
                                   OR "SupportsRerank"
                              THEN '["text"]'::jsonb ELSE '[]'::jsonb END)
                        || (CASE WHEN "SupportsImageGeneration"
                                 THEN '["image"]'::jsonb ELSE '[]'::jsonb END)
                        || (CASE WHEN "SupportsVideoGeneration"
                                 THEN '["video"]'::jsonb ELSE '[]'::jsonb END)
                        || (CASE WHEN "SupportsTextToSpeech"
                                 THEN '["audio"]'::jsonb ELSE '[]'::jsonb END),
                    "CapabilitySource" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapabilitiesLastVerifiedAt",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "CapabilitySource",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "InputModalities",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "OutputModalities",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "CapabilitiesLastVerifiedAt",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "CapabilitySource",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "InputModalities",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "OperationalCapabilities",
                table: "ModelIdentifiers");

            migrationBuilder.DropColumn(
                name: "OutputModalities",
                table: "ModelIdentifiers");
        }
    }
}

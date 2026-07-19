using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderMetadataSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderMetadataDriftItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelProviderMappingId = table.Column<int>(type: "integer", nullable: false),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    OpenRouterModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DriftType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentValuesJson = table.Column<string>(type: "text", nullable: false),
                    ProposedValuesJson = table.Column<string>(type: "text", nullable: false),
                    FirstDetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastDetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncRunId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderMetadataDriftItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderMetadataDriftItems_ModelProviderMappings_ModelProvi~",
                        column: x => x.ModelProviderMappingId,
                        principalTable: "ModelProviderMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderMetadataSyncRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModelsFetched = table.Column<int>(type: "integer", nullable: false),
                    MappingsChecked = table.Column<int>(type: "integer", nullable: false),
                    ItemsCreated = table.Column<int>(type: "integer", nullable: false),
                    ItemsUpdated = table.Column<int>(type: "integer", nullable: false),
                    ItemsAutoResolved = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderMetadataSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderMetadataDriftItems_Mapping_Type_Pending",
                table: "ProviderMetadataDriftItems",
                columns: new[] { "ModelProviderMappingId", "DriftType" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderMetadataDriftItems_ProviderId",
                table: "ProviderMetadataDriftItems",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderMetadataDriftItems_Status_Type",
                table: "ProviderMetadataDriftItems",
                columns: new[] { "Status", "DriftType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderMetadataDriftItems");

            migrationBuilder.DropTable(
                name: "ProviderMetadataSyncRuns");
        }
    }
}

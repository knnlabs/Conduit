using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RefactorModelProviderMappingToUseAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelCostMapping");

            // Check if indexes exist before dropping
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_ModelProviderMapping_CapabilityOverrides"";
                DROP INDEX IF EXISTS ""IX_ModelProviderMapping_ModelId_QualityScore"";
            ");

            migrationBuilder.DropColumn(
                name: "CapabilityOverrides",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "DefaultCapabilityType",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "ProviderVariation",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "QualityScore",
                table: "ModelProviderMappings");

            migrationBuilder.AddColumn<int>(
                name: "ModelProviderTypeAssociationId",
                table: "ModelProviderMappings",
                type: "integer",
                nullable: true);

            // Data migration: Attempt to match existing mappings to ModelProviderTypeAssociations
            // This matches based on ModelId and ProviderModelId (identifier)
            migrationBuilder.Sql(@"
                UPDATE ""ModelProviderMappings"" mpm
                SET ""ModelProviderTypeAssociationId"" = mpta.""Id""
                FROM ""ModelIdentifiers"" mpta
                WHERE mpm.""ModelId"" = mpta.""ModelId""
                  AND mpm.""ProviderModelId"" = mpta.""Identifier""
                  AND mpm.""ModelProviderTypeAssociationId"" IS NULL
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMapping_ModelProviderTypeAssociationId",
                table: "ModelProviderMappings",
                column: "ModelProviderTypeAssociationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelProviderMappings_ModelIdentifiers_ModelProviderTypeAss~",
                table: "ModelProviderMappings",
                column: "ModelProviderTypeAssociationId",
                principalTable: "ModelIdentifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelProviderMappings_ModelIdentifiers_ModelProviderTypeAss~",
                table: "ModelProviderMappings");

            migrationBuilder.DropIndex(
                name: "IX_ModelProviderMapping_ModelProviderTypeAssociationId",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "ModelProviderTypeAssociationId",
                table: "ModelProviderMappings");

            migrationBuilder.AddColumn<string>(
                name: "CapabilityOverrides",
                table: "ModelProviderMappings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCapabilityType",
                table: "ModelProviderMappings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProviderVariation",
                table: "ModelProviderMappings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QualityScore",
                table: "ModelProviderMappings",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelCostMapping",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelCostId = table.Column<int>(type: "integer", nullable: false),
                    ModelProviderTypeAssociationId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ModelProviderMappingId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCostMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelCostMapping_ModelCosts_ModelCostId",
                        column: x => x.ModelCostId,
                        principalTable: "ModelCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelCostMapping_ModelIdentifiers_ModelProviderTypeAssociat~",
                        column: x => x.ModelProviderTypeAssociationId,
                        principalTable: "ModelIdentifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelCostMapping_ModelProviderMappings_ModelProviderMapping~",
                        column: x => x.ModelProviderMappingId,
                        principalTable: "ModelProviderMappings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMapping_CapabilityOverrides",
                table: "ModelProviderMappings",
                column: "CapabilityOverrides",
                filter: "\"CapabilityOverrides\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMapping_ModelId_QualityScore",
                table: "ModelProviderMappings",
                columns: new[] { "ModelId", "QualityScore" },
                filter: "\"QualityScore\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCostMapping_ModelCostId",
                table: "ModelCostMapping",
                column: "ModelCostId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCostMapping_ModelProviderMappingId",
                table: "ModelCostMapping",
                column: "ModelProviderMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCostMapping_ModelProviderTypeAssociationId",
                table: "ModelCostMapping",
                column: "ModelProviderTypeAssociationId");
        }
    }
}

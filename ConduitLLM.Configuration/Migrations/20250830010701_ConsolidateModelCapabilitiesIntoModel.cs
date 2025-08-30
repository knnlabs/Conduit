using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateModelCapabilitiesIntoModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Models_ModelCapabilities_ModelCapabilitiesId",
                table: "Models");

            migrationBuilder.DropTable(
                name: "ModelCapabilities");

            migrationBuilder.DropIndex(
                name: "IX_Model_ModelCapabilitiesId",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "MaxContextTokensOverride",
                table: "ModelProviderMappings");

            migrationBuilder.RenameColumn(
                name: "ModelCapabilitiesId",
                table: "Models",
                newName: "TokenizerType");

            migrationBuilder.AddColumn<int>(
                name: "MaxInputTokens",
                table: "Models",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxOutputTokens",
                table: "Models",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsChat",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsEmbeddings",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsFunctionCalling",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsImageGeneration",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsStreaming",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsVideoGeneration",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsVision",
                table: "Models",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxInputTokens",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "MaxOutputTokens",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsChat",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsEmbeddings",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsFunctionCalling",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsImageGeneration",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsStreaming",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsVideoGeneration",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "SupportsVision",
                table: "Models");

            migrationBuilder.RenameColumn(
                name: "TokenizerType",
                table: "Models",
                newName: "ModelCapabilitiesId");

            migrationBuilder.AddColumn<int>(
                name: "MaxContextTokensOverride",
                table: "ModelProviderMappings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelCapabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    MinTokens = table.Column<int>(type: "integer", nullable: false),
                    SupportsChat = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsEmbeddings = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsFunctionCalling = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsImageGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVideoGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVision = table.Column<bool>(type: "boolean", nullable: false),
                    TokenizerType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCapabilities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Model_ModelCapabilitiesId",
                table: "Models",
                column: "ModelCapabilitiesId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCapabilities_Chat_Function_Streaming",
                table: "ModelCapabilities",
                columns: new[] { "SupportsChat", "SupportsFunctionCalling", "SupportsStreaming" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCapabilities_SupportsChat",
                table: "ModelCapabilities",
                column: "SupportsChat",
                filter: "\"SupportsChat\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCapabilities_SupportsFunctionCalling",
                table: "ModelCapabilities",
                column: "SupportsFunctionCalling",
                filter: "\"SupportsFunctionCalling\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCapabilities_SupportsImageGeneration",
                table: "ModelCapabilities",
                column: "SupportsImageGeneration",
                filter: "\"SupportsImageGeneration\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCapabilities_SupportsVideoGeneration",
                table: "ModelCapabilities",
                column: "SupportsVideoGeneration",
                filter: "\"SupportsVideoGeneration\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCapabilities_SupportsVision",
                table: "ModelCapabilities",
                column: "SupportsVision",
                filter: "\"SupportsVision\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_Models_ModelCapabilities_ModelCapabilitiesId",
                table: "Models",
                column: "ModelCapabilitiesId",
                principalTable: "ModelCapabilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

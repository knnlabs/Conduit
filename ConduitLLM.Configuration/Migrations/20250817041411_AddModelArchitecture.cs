using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddModelArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportedFormats",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsAudioTranscription",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsChat",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsEmbeddings",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsFunctionCalling",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsImageGeneration",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsRealtimeAudio",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsStreaming",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsTextToSpeech",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsVideoGeneration",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "SupportsVision",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "TokenizerType",
                table: "ModelProviderMappings");

            migrationBuilder.RenameColumn(
                name: "SupportedVoices",
                table: "ModelProviderMappings",
                newName: "ProviderVariation");

            migrationBuilder.RenameColumn(
                name: "SupportedLanguages",
                table: "ModelProviderMappings",
                newName: "CapabilityOverrides");

            migrationBuilder.RenameColumn(
                name: "MaxContextTokens",
                table: "ModelProviderMappings",
                newName: "MaxContextTokensOverride");

            migrationBuilder.AddColumn<int>(
                name: "ModelId",
                table: "ModelProviderMappings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "QualityScore",
                table: "ModelProviderMappings",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelAuthor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelAuthor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelCapabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    MinTokens = table.Column<int>(type: "integer", nullable: false),
                    SupportsVision = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsAudioTranscription = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsTextToSpeech = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsRealtimeAudio = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsImageGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVideoGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsEmbeddings = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsChat = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsFunctionCalling = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    TokenizerType = table.Column<int>(type: "integer", nullable: false),
                    SupportedVoices = table.Column<string>(type: "text", nullable: true),
                    SupportedLanguages = table.Column<string>(type: "text", nullable: true),
                    SupportedFormats = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCapabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthorId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TokenizerType = table.Column<int>(type: "integer", nullable: false),
                    Parameters = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelSeries_ModelAuthor_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "ModelAuthor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Model",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ModelCardUrl = table.Column<string>(type: "text", nullable: true),
                    ModelType = table.Column<int>(type: "integer", nullable: false),
                    ModelSeriesId = table.Column<int>(type: "integer", nullable: false),
                    ModelCapabilitiesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Model", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Model_ModelCapabilities_ModelCapabilitiesId",
                        column: x => x.ModelCapabilitiesId,
                        principalTable: "ModelCapabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Model_ModelSeries_ModelSeriesId",
                        column: x => x.ModelSeriesId,
                        principalTable: "ModelSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelIdentifier",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelId = table.Column<int>(type: "integer", nullable: false),
                    Identifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelIdentifier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelIdentifier_Model_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Model",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMappings_ModelId",
                table: "ModelProviderMappings",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Model_ModelCapabilitiesId",
                table: "Model",
                column: "ModelCapabilitiesId");

            migrationBuilder.CreateIndex(
                name: "IX_Model_ModelSeriesId",
                table: "Model",
                column: "ModelSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifier_ModelId",
                table: "ModelIdentifier",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelSeries_AuthorId",
                table: "ModelSeries",
                column: "AuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelProviderMappings_Model_ModelId",
                table: "ModelProviderMappings",
                column: "ModelId",
                principalTable: "Model",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelProviderMappings_Model_ModelId",
                table: "ModelProviderMappings");

            migrationBuilder.DropTable(
                name: "ModelIdentifier");

            migrationBuilder.DropTable(
                name: "Model");

            migrationBuilder.DropTable(
                name: "ModelCapabilities");

            migrationBuilder.DropTable(
                name: "ModelSeries");

            migrationBuilder.DropTable(
                name: "ModelAuthor");

            migrationBuilder.DropIndex(
                name: "IX_ModelProviderMappings_ModelId",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "QualityScore",
                table: "ModelProviderMappings");

            migrationBuilder.RenameColumn(
                name: "ProviderVariation",
                table: "ModelProviderMappings",
                newName: "SupportedVoices");

            migrationBuilder.RenameColumn(
                name: "MaxContextTokensOverride",
                table: "ModelProviderMappings",
                newName: "MaxContextTokens");

            migrationBuilder.RenameColumn(
                name: "CapabilityOverrides",
                table: "ModelProviderMappings",
                newName: "SupportedLanguages");

            migrationBuilder.AddColumn<string>(
                name: "SupportedFormats",
                table: "ModelProviderMappings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsAudioTranscription",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsChat",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsEmbeddings",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsFunctionCalling",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsImageGeneration",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsRealtimeAudio",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsStreaming",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsTextToSpeech",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsVideoGeneration",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsVision",
                table: "ModelProviderMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TokenizerType",
                table: "ModelProviderMappings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}

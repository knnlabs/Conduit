using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OperationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CostUnit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CostPerUnit = table.Column<decimal>(type: "decimal(10, 6)", nullable: false),
                    MinimumCharge = table.Column<decimal>(type: "decimal(10, 6)", nullable: true),
                    AdditionalFactors = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioCosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderCredentialId = table.Column<int>(type: "INTEGER", nullable: false),
                    TranscriptionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultTranscriptionModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TextToSpeechEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultTTSModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DefaultTTSVoice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RealtimeEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultRealtimeModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RealtimeEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CustomSettings = table.Column<string>(type: "TEXT", nullable: true),
                    RoutingPriority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioProviderConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioProviderConfigs_ProviderCredentials_ProviderCredentialId",
                        column: x => x.ProviderCredentialId,
                        principalTable: "ProviderCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioUsageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VirtualKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OperationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RequestId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: true),
                    CharacterCount = table.Column<int>(type: "INTEGER", nullable: true),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(10, 6)", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Voice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioUsageLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioCosts_EffectiveFrom_EffectiveTo",
                table: "AudioCosts",
                columns: new[] { "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioCosts_Provider_OperationType_Model_IsActive",
                table: "AudioCosts",
                columns: new[] { "Provider", "OperationType", "Model", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioProviderConfigs_ProviderCredentialId",
                table: "AudioProviderConfigs",
                column: "ProviderCredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioUsageLogs_Provider_OperationType",
                table: "AudioUsageLogs",
                columns: new[] { "Provider", "OperationType" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioUsageLogs_SessionId",
                table: "AudioUsageLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioUsageLogs_Timestamp",
                table: "AudioUsageLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AudioUsageLogs_VirtualKey",
                table: "AudioUsageLogs",
                column: "VirtualKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioCosts");

            migrationBuilder.DropTable(
                name: "AudioProviderConfigs");

            migrationBuilder.DropTable(
                name: "AudioUsageLogs");
        }
    }
}

using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class _2025Apr19 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelIdPattern = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    InputTokenCost = table.Column<decimal>(type: "decimal(18, 10)", nullable: false),
                    OutputTokenCost = table.Column<decimal>(type: "decimal(18, 10)", nullable: false),
                    EmbeddingTokenCost = table.Column<decimal>(type: "decimal(18, 10)", nullable: true),
                    ImageCostPerImage = table.Column<decimal>(type: "decimal(18, 4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ApiVersion = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxBudget = table.Column<decimal>(type: "decimal(18, 8)", nullable: true),
                    CurrentSpend = table.Column<decimal>(type: "decimal(18, 8)", nullable: false),
                    BudgetDuration = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BudgetStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    AllowedModels = table.Column<string>(type: "TEXT", nullable: true),
                    RateLimitRpm = table.Column<int>(type: "INTEGER", nullable: true),
                    RateLimitRpd = table.Column<int>(type: "INTEGER", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelProviderMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelAlias = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProviderModelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProviderCredentialId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxContextTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelProviderMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelProviderMappings_ProviderCredentials_ProviderCredentialId",
                        column: x => x.ProviderCredentialId,
                        principalTable: "ProviderCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VirtualKeyId = table.Column<int>(type: "INTEGER", nullable: true),
                    VirtualKeyId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_VirtualKeys_VirtualKeyId1",
                        column: x => x.VirtualKeyId1,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RequestLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VirtualKeyId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RequestType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(10, 6)", nullable: false),
                    ResponseTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ClientIp = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RequestPath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestLogs_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VirtualKeySpendHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VirtualKeyId = table.Column<int>(type: "INTEGER", nullable: false),
                    VirtualKeyId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10, 6)", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeySpendHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualKeySpendHistory_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VirtualKeySpendHistory_VirtualKeys_VirtualKeyId1",
                        column: x => x.VirtualKeyId1,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalSettings_Key",
                table: "GlobalSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelCosts_ModelIdPattern",
                table: "ModelCosts",
                column: "ModelIdPattern");

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMappings_ModelAlias_ProviderCredentialId",
                table: "ModelProviderMappings",
                columns: new[] { "ModelAlias", "ProviderCredentialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMappings_ProviderCredentialId",
                table: "ModelProviderMappings",
                column: "ProviderCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_VirtualKeyId",
                table: "Notifications",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_VirtualKeyId1",
                table: "Notifications",
                column: "VirtualKeyId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCredentials_ProviderName",
                table: "ProviderCredentials",
                column: "ProviderName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_VirtualKeyId",
                table: "RequestLogs",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeys_KeyHash",
                table: "VirtualKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeySpendHistory_VirtualKeyId",
                table: "VirtualKeySpendHistory",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeySpendHistory_VirtualKeyId1",
                table: "VirtualKeySpendHistory",
                column: "VirtualKeyId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalSettings");

            migrationBuilder.DropTable(
                name: "ModelCosts");

            migrationBuilder.DropTable(
                name: "ModelProviderMappings");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "RequestLogs");

            migrationBuilder.DropTable(
                name: "VirtualKeySpendHistory");

            migrationBuilder.DropTable(
                name: "ProviderCredentials");

            migrationBuilder.DropTable(
                name: "VirtualKeys");
        }
    }
}

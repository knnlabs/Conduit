using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidatedDbContexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RouterConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DefaultRoutingStrategy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryBaseDelayMs = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryMaxDelayMs = table.Column<int>(type: "INTEGER", nullable: false),
                    FallbacksEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouterConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FallbackConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrimaryModelDeploymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RouterConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FallbackConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FallbackConfigurations_RouterConfigurations_RouterConfigId",
                        column: x => x.RouterConfigId,
                        principalTable: "RouterConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    HealthCheckEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RPM = table.Column<int>(type: "INTEGER", nullable: true),
                    TPM = table.Column<int>(type: "INTEGER", nullable: true),
                    InputTokenCostPer1K = table.Column<decimal>(type: "decimal(18, 8)", nullable: true),
                    OutputTokenCostPer1K = table.Column<decimal>(type: "decimal(18, 8)", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHealthy = table.Column<bool>(type: "INTEGER", nullable: false),
                    RouterConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelDeployments_RouterConfigurations_RouterConfigId",
                        column: x => x.RouterConfigId,
                        principalTable: "RouterConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FallbackModelMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FallbackConfigurationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelDeploymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FallbackModelMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FallbackModelMappings_FallbackConfigurations_FallbackConfigurationId",
                        column: x => x.FallbackConfigurationId,
                        principalTable: "FallbackConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FallbackConfigurations_PrimaryModelDeploymentId",
                table: "FallbackConfigurations",
                column: "PrimaryModelDeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackConfigurations_RouterConfigId",
                table: "FallbackConfigurations",
                column: "RouterConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackModelMappings_FallbackConfigurationId_ModelDeploymentId",
                table: "FallbackModelMappings",
                columns: new[] { "FallbackConfigurationId", "ModelDeploymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FallbackModelMappings_FallbackConfigurationId_Order",
                table: "FallbackModelMappings",
                columns: new[] { "FallbackConfigurationId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_IsEnabled",
                table: "ModelDeployments",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_IsHealthy",
                table: "ModelDeployments",
                column: "IsHealthy");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_ModelName",
                table: "ModelDeployments",
                column: "ModelName");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_ProviderName",
                table: "ModelDeployments",
                column: "ProviderName");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_RouterConfigId",
                table: "ModelDeployments",
                column: "RouterConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_RouterConfigurations_LastUpdated",
                table: "RouterConfigurations",
                column: "LastUpdated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FallbackModelMappings");

            migrationBuilder.DropTable(
                name: "ModelDeployments");

            migrationBuilder.DropTable(
                name: "FallbackConfigurations");

            migrationBuilder.DropTable(
                name: "RouterConfigurations");
        }
    }
}

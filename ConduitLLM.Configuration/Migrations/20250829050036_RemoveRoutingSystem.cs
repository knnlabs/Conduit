using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRoutingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key constraints first
            // Check if tables exist before dropping constraints
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'FallbackModelMappings') THEN
                        ALTER TABLE ""FallbackModelMappings"" DROP CONSTRAINT IF EXISTS ""FK_FallbackModelMappings_FallbackConfigurations_FallbackConfi~"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'FallbackConfigurations') THEN
                        ALTER TABLE ""FallbackConfigurations"" DROP CONSTRAINT IF EXISTS ""FK_FallbackConfigurations_RouterConfigurations_RouterConfigId"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ModelDeployments') THEN
                        ALTER TABLE ""ModelDeployments"" DROP CONSTRAINT IF EXISTS ""FK_ModelDeployments_Providers_ProviderId"";
                        ALTER TABLE ""ModelDeployments"" DROP CONSTRAINT IF EXISTS ""FK_ModelDeployments_RouterConfigurations_RouterConfigId"";
                    END IF;
                END $$;
            ");

            // Drop tables in correct order (respecting dependencies)
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"FallbackModelMappings\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"FallbackConfigurations\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ModelDeployments\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"RouterConfigurations\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate RouterConfigurations table
            migrationBuilder.CreateTable(
                name: "RouterConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DefaultRoutingStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    RetryBaseDelayMs = table.Column<int>(type: "integer", nullable: false),
                    RetryMaxDelayMs = table.Column<int>(type: "integer", nullable: false),
                    FallbacksEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouterConfigurations", x => x.Id);
                });

            // Recreate ModelDeployments table
            migrationBuilder.CreateTable(
                name: "ModelDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    HealthCheckEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RPM = table.Column<int>(type: "integer", nullable: true),
                    TPM = table.Column<int>(type: "integer", nullable: true),
                    InputTokenCostPer1K = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    OutputTokenCostPer1K = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    RouterConfigId = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeploymentName = table.Column<string>(type: "text", nullable: false),
                    SupportsEmbeddings = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelDeployments_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelDeployments_RouterConfigurations_RouterConfigId",
                        column: x => x.RouterConfigId,
                        principalTable: "RouterConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Recreate FallbackConfigurations table
            migrationBuilder.CreateTable(
                name: "FallbackConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryModelDeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouterConfigId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            // Recreate FallbackModelMappings table
            migrationBuilder.CreateTable(
                name: "FallbackModelMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FallbackConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelDeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FallbackModelMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FallbackModelMappings_FallbackConfigurations_FallbackConfi~",
                        column: x => x.FallbackConfigurationId,
                        principalTable: "FallbackConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_RouterConfigurations_LastUpdated",
                table: "RouterConfigurations",
                column: "LastUpdated");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_ModelName",
                table: "ModelDeployments",
                column: "ModelName");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_ProviderId",
                table: "ModelDeployments",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_IsEnabled",
                table: "ModelDeployments",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_IsHealthy",
                table: "ModelDeployments",
                column: "IsHealthy");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_RouterConfigId",
                table: "ModelDeployments",
                column: "RouterConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackConfigurations_PrimaryModelDeploymentId",
                table: "FallbackConfigurations",
                column: "PrimaryModelDeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackConfigurations_RouterConfigId",
                table: "FallbackConfigurations",
                column: "RouterConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackModelMappings_FallbackConfigurationId_Order",
                table: "FallbackModelMappings",
                columns: new[] { "FallbackConfigurationId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FallbackModelMappings_FallbackConfigurationId_ModelDeploym~",
                table: "FallbackModelMappings",
                columns: new[] { "FallbackConfigurationId", "ModelDeploymentId" },
                unique: true);
        }
    }
}

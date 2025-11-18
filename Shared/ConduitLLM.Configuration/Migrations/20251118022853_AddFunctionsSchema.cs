using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FunctionConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    DefaultExecutionMode = table.Column<int>(type: "integer", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    MaxRetries = table.Column<int>(type: "integer", nullable: true),
                    ProviderSettings = table.Column<string>(type: "jsonb", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FunctionCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CostName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PricingModel = table.Column<int>(type: "integer", nullable: false),
                    CostPerExecution = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CostPerResult = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CostPerToken = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CostPerMinute = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    TieredPricing = table.Column<string>(type: "jsonb", nullable: true),
                    PricingConfiguration = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionCosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FunctionCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FunctionConfigurationId = table.Column<int>(type: "integer", nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CredentialGroup = table.Column<short>(type: "smallint", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CredentialName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FunctionCredentials_FunctionConfigurations_FunctionConfigur~",
                        column: x => x.FunctionConfigurationId,
                        principalTable: "FunctionConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FunctionExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FunctionConfigurationId = table.Column<int>(type: "integer", nullable: false),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    ExecutionMode = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RequestJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CostCalculationDetails = table.Column<string>(type: "jsonb", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeasedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeaseExpiryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    WebhookUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WebhookDelivered = table.Column<bool>(type: "boolean", nullable: false),
                    ProgressPercentage = table.Column<int>(type: "integer", nullable: true),
                    StatusMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FunctionExecutions_FunctionConfigurations_FunctionConfigura~",
                        column: x => x.FunctionConfigurationId,
                        principalTable: "FunctionConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FunctionCostMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FunctionConfigurationId = table.Column<int>(type: "integer", nullable: false),
                    FunctionCostId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionCostMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FunctionCostMappings_FunctionConfigurations_FunctionConfigu~",
                        column: x => x.FunctionConfigurationId,
                        principalTable: "FunctionConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FunctionCostMappings_FunctionCosts_FunctionCostId",
                        column: x => x.FunctionCostId,
                        principalTable: "FunctionCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FunctionExecutionAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FunctionExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    EventDetails = table.Column<string>(type: "jsonb", nullable: true),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionExecutionAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FunctionExecutionAudits_FunctionExecutions_FunctionExecutio~",
                        column: x => x.FunctionExecutionId,
                        principalTable: "FunctionExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCostMappings_FunctionConfigurationId",
                table: "FunctionCostMappings",
                column: "FunctionConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCostMappings_FunctionCostId",
                table: "FunctionCostMappings",
                column: "FunctionCostId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredentials_FunctionConfigurationId",
                table: "FunctionCredentials",
                column: "FunctionConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecutionAudits_FunctionExecutionId",
                table: "FunctionExecutionAudits",
                column: "FunctionExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecutions_FunctionConfigurationId",
                table: "FunctionExecutions",
                column: "FunctionConfigurationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FunctionCostMappings");

            migrationBuilder.DropTable(
                name: "FunctionCredentials");

            migrationBuilder.DropTable(
                name: "FunctionExecutionAudits");

            migrationBuilder.DropTable(
                name: "FunctionCosts");

            migrationBuilder.DropTable(
                name: "FunctionExecutions");

            migrationBuilder.DropTable(
                name: "FunctionConfigurations");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using System;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProviderHealthMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key constraints first
            migrationBuilder.DropForeignKey(
                name: "FK_ProviderHealthConfigurations_Providers_ProviderId",
                table: "ProviderHealthConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProviderHealthRecords_Providers_ProviderId",
                table: "ProviderHealthRecords");

            // Drop tables
            migrationBuilder.DropTable(
                name: "ProviderHealthConfigurations");

            migrationBuilder.DropTable(
                name: "ProviderHealthRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate ProviderHealthConfigurations table
            migrationBuilder.CreateTable(
                name: "ProviderHealthConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    MonitoringEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CheckIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    ConsecutiveFailuresThreshold = table.Column<int>(type: "integer", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CustomEndpointUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastCheckedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderHealthConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderHealthConfigurations_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Recreate ProviderHealthRecords table
            migrationBuilder.CreateTable(
                name: "ProviderHealthRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    EndpointUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderHealthRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderHealthRecords_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_ProviderHealthConfigurations_ProviderId",
                table: "ProviderHealthConfigurations",
                column: "ProviderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHealthRecords_ProviderId_TimestampUtc",
                table: "ProviderHealthRecords",
                columns: new[] { "ProviderId", "TimestampUtc" });
        }
    }
}
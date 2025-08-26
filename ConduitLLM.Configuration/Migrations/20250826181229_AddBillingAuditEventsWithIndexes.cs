using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingAuditEventsWithIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiParameters",
                table: "ModelSeries");

            migrationBuilder.DropColumn(
                name: "ApiParameters",
                table: "ModelProviderMappings");

            migrationBuilder.RenameColumn(
                name: "ApiParameters",
                table: "Models",
                newName: "Parameters");

            migrationBuilder.CreateTable(
                name: "BillingAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UsageJson = table.Column<string>(type: "jsonb", nullable: true),
                    CalculatedCost = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProviderType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingAuditEvents_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_EventType",
                table: "BillingAuditEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_EventType_Timestamp",
                table: "BillingAuditEvents",
                columns: new[] { "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_RequestId",
                table: "BillingAuditEvents",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_Timestamp",
                table: "BillingAuditEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_VirtualKeyId",
                table: "BillingAuditEvents",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_VirtualKeyId_Timestamp",
                table: "BillingAuditEvents",
                columns: new[] { "VirtualKeyId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingAuditEvents");

            migrationBuilder.RenameColumn(
                name: "Parameters",
                table: "Models",
                newName: "ApiParameters");

            migrationBuilder.AddColumn<string>(
                name: "ApiParameters",
                table: "ModelSeries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiParameters",
                table: "ModelProviderMappings",
                type: "text",
                nullable: true);
        }
    }
}

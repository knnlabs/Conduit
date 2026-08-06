using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BillingWindowStartUtc",
                table: "VirtualKeyGroupTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BilledAtUtc",
                table: "RequestLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderCostMarkupMultiplier",
                table: "RequestLogs",
                type: "numeric(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VirtualKeyGroupId",
                table: "BillingAuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingReconciliationCheckpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NextWindowStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingReconciliationCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_BillingWindowStartUtc_VirtualKe~",
                table: "VirtualKeyGroupTransactions",
                columns: new[] { "BillingWindowStartUtc", "VirtualKeyGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_BilledAtUtc_VirtualKeyId",
                table: "RequestLogs",
                columns: new[] { "BilledAtUtc", "VirtualKeyId" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_VirtualKeyGroupId",
                table: "BillingAuditEvents",
                column: "VirtualKeyGroupId");

            migrationBuilder.CreateIndex(
                name: "UX_BillingAuditEvents_ReconciliationRequestId",
                table: "BillingAuditEvents",
                columns: new[] { "EventType", "RequestId" },
                unique: true,
                filter: "\"EventType\" = 15");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingReconciliationCheckpoints");

            migrationBuilder.DropIndex(
                name: "IX_VirtualKeyGroupTransactions_BillingWindowStartUtc_VirtualKe~",
                table: "VirtualKeyGroupTransactions");

            migrationBuilder.DropIndex(
                name: "IX_RequestLogs_BilledAtUtc_VirtualKeyId",
                table: "RequestLogs");

            migrationBuilder.DropIndex(
                name: "IX_BillingAuditEvents_VirtualKeyGroupId",
                table: "BillingAuditEvents");

            migrationBuilder.DropIndex(
                name: "UX_BillingAuditEvents_ReconciliationRequestId",
                table: "BillingAuditEvents");

            migrationBuilder.DropColumn(
                name: "BillingWindowStartUtc",
                table: "VirtualKeyGroupTransactions");

            migrationBuilder.DropColumn(
                name: "BilledAtUtc",
                table: "RequestLogs");

            migrationBuilder.DropColumn(
                name: "ProviderCostMarkupMultiplier",
                table: "RequestLogs");

            migrationBuilder.DropColumn(
                name: "VirtualKeyGroupId",
                table: "BillingAuditEvents");
        }
    }
}

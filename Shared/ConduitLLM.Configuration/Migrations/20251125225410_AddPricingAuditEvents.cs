using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModelCostId = table.Column<int>(type: "integer", nullable: false),
                    PricingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InputParameters = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    MatchedRule = table.Column<string>(type: "jsonb", nullable: true),
                    UsedDefaultRate = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedRate = table.Column<decimal>(type: "numeric(10,8)", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    CalculatedCost = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingAuditEvents_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Note: Removed partial index filter as PostgreSQL requires IMMUTABLE functions in index predicates
            // The cleanup service uses a WHERE clause at query time instead
            migrationBuilder.CreateIndex(
                name: "IX_PricingAuditEvents_Timestamp",
                table: "PricingAuditEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAuditEvents_ModelId",
                table: "PricingAuditEvents",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAuditEvents_ModelId_Timestamp",
                table: "PricingAuditEvents",
                columns: new[] { "ModelId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingAuditEvents_PricingType",
                table: "PricingAuditEvents",
                column: "PricingType");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAuditEvents_PricingType_Timestamp",
                table: "PricingAuditEvents",
                columns: new[] { "PricingType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingAuditEvents_RequestId",
                table: "PricingAuditEvents",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAuditEvents_VirtualKeyId",
                table: "PricingAuditEvents",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAuditEvents_VirtualKeyId_Timestamp",
                table: "PricingAuditEvents",
                columns: new[] { "VirtualKeyId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingAuditEvents");
        }
    }
}

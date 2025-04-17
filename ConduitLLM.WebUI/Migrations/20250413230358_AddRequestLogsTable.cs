using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.WebUI.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    Cost = table.Column<decimal>(type: "decimal(10, 6)", precision: 10, scale: 6, nullable: false),
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_ModelName",
                table: "RequestLogs",
                column: "ModelName");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_RequestType",
                table: "RequestLogs",
                column: "RequestType");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_Timestamp",
                table: "RequestLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_VirtualKeyId",
                table: "RequestLogs",
                column: "VirtualKeyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestLogs");
        }
    }
}

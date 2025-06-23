using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddAsyncTaskTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AsyncTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Result = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VirtualKeyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsyncTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsyncTasks_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_CreatedAt",
                table: "AsyncTasks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_IsArchived",
                table: "AsyncTasks",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_State",
                table: "AsyncTasks",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_Type",
                table: "AsyncTasks",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_VirtualKeyId",
                table: "AsyncTasks",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_VirtualKeyId_CreatedAt",
                table: "AsyncTasks",
                columns: new[] { "VirtualKeyId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsyncTasks");
        }
    }
}

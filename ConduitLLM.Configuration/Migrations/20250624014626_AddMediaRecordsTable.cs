using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    VirtualKeyId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Prompt = table.Column<string>(type: "TEXT", nullable: true),
                    StorageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PublicUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccessCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaRecords_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_CreatedAt",
                table: "MediaRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_ExpiresAt",
                table: "MediaRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_StorageKey",
                table: "MediaRecords",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_VirtualKeyId",
                table: "MediaRecords",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_VirtualKeyId_CreatedAt",
                table: "MediaRecords",
                columns: new[] { "VirtualKeyId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaRecords");
        }
    }
}

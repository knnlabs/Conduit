using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportsEmbeddingsToModelDeployment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SupportsEmbeddings",
                table: "ModelDeployments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Result",
                table: "AsyncTasks",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "AsyncTasks",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Metadata",
                table: "AsyncTasks",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "AsyncTasks",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "MediaLifecycleRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VirtualKeyId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", nullable: false),
                    MediaUrl = table.Column<string>(type: "TEXT", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedByModel = table.Column<string>(type: "TEXT", nullable: false),
                    GenerationPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaLifecycleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaLifecycleRecords_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_CreatedAt",
                table: "MediaLifecycleRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_ExpiresAt",
                table: "MediaLifecycleRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_ExpiresAt_IsDeleted",
                table: "MediaLifecycleRecords",
                columns: new[] { "ExpiresAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_StorageKey",
                table: "MediaLifecycleRecords",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_VirtualKeyId",
                table: "MediaLifecycleRecords",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_VirtualKeyId_IsDeleted",
                table: "MediaLifecycleRecords",
                columns: new[] { "VirtualKeyId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaLifecycleRecords");

            migrationBuilder.DropColumn(
                name: "SupportsEmbeddings",
                table: "ModelDeployments");

            migrationBuilder.AlterColumn<string>(
                name: "Result",
                table: "AsyncTasks",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "AsyncTasks",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Metadata",
                table: "AsyncTasks",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "AsyncTasks",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}

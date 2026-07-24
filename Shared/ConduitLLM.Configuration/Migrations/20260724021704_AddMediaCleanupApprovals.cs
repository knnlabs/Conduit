using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaCleanupApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaCleanupApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CleanupType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VirtualKeyGroupId = table.Column<int>(type: "integer", nullable: true),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    CandidateBytes = table.Column<long>(type: "bigint", nullable: false),
                    CutoffUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecisionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExecutionStatus = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FilesDeleted = table.Column<int>(type: "integer", nullable: false),
                    RecordsTombstoned = table.Column<int>(type: "integer", nullable: false),
                    Failures = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaCleanupApprovals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaCleanupApprovals_CleanupType_VirtualKeyGroupId_Status",
                table: "MediaCleanupApprovals",
                columns: new[] { "CleanupType", "VirtualKeyGroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaCleanupApprovals_ExpiresAtUtc",
                table: "MediaCleanupApprovals",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MediaCleanupApprovals_Status",
                table: "MediaCleanupApprovals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaCleanupApprovals");
        }
    }
}

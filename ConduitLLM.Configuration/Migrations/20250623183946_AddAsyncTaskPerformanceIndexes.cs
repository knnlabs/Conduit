using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddAsyncTaskPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_Archival",
                table: "AsyncTasks",
                columns: new[] { "IsArchived", "CompletedAt", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_Cleanup",
                table: "AsyncTasks",
                columns: new[] { "IsArchived", "ArchivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AsyncTasks_Archival",
                table: "AsyncTasks");

            migrationBuilder.DropIndex(
                name: "IX_AsyncTasks_Cleanup",
                table: "AsyncTasks");
        }
    }
}

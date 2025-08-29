using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSnapshotAfterAudioRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These columns were already dropped in the RemoveAudioColumns migration
            // This migration exists only to update the model snapshot
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: The columns were removed in RemoveAudioColumns migration
            // Reverting this empty migration should not re-add them
        }
    }
}

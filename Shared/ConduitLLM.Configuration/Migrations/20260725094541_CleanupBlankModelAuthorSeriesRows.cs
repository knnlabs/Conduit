using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <summary>
    /// Data-only cleanup for issue #1192: POST /v1/admin/models used to cascade-insert a
    /// blank ModelSeries and blank ModelAuthor (phantom navigation initializers attached as
    /// Added by the graph-traversing DbSet.Add). Removes the junk blank-name rows that are
    /// not referenced by any surviving row; referenced ones are left untouched because the
    /// original intended series cannot be recovered automatically.
    /// </summary>
    public partial class CleanupBlankModelAuthorSeriesRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "ModelSeries" s
                WHERE s."Name" = ''
                  AND NOT EXISTS (SELECT 1 FROM "Models" m WHERE m."ModelSeriesId" = s."Id");
                """);

            migrationBuilder.Sql("""
                DELETE FROM "ModelAuthors" a
                WHERE a."Name" = ''
                  AND NOT EXISTS (SELECT 1 FROM "ModelSeries" s WHERE s."AuthorId" = a."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only cleanup of junk rows; nothing to restore.
        }
    }
}

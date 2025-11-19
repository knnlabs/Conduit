using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateRAGFunctionPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Consolidate RAG_Search (4), RAG_Save (5), RAG_Delete (6), and RAG_Update (7) into RAG (4)
            // Update any existing function configurations with RAG_Save, RAG_Delete, or RAG_Update to RAG
            migrationBuilder.Sql(@"
                UPDATE ""FunctionConfigurations""
                SET ""Purpose"" = 4
                WHERE ""Purpose"" IN (5, 6, 7);
            ");

            // Note: FunctionExecutions table does not have a Purpose column.
            // Purpose is tracked via the FunctionConfiguration relationship.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No way to reverse this migration as we don't know which specific RAG type each record was
            // Leave all as RAG (4) - manual intervention required if rollback is needed
        }
    }
}

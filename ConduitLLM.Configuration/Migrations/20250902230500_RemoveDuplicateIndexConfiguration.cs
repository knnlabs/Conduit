using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicateIndexConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if index exists before trying to drop it
            // The index may not exist if it was never created due to configuration changes
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_ModelIdentifiers_ModelId_Identifier_Provider"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifiers_ModelId_Identifier_Provider",
                table: "ModelIdentifiers",
                columns: new[] { "ModelId", "Identifier", "Provider" },
                unique: true);
        }
    }
}

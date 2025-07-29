using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexForSupportsChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMappings_SupportsChat_IsEnabled",
                table: "ModelProviderMappings",
                columns: new[] { "SupportsChat", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelProviderMappings_SupportsChat_IsEnabled",
                table: "ModelProviderMappings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.WebUI.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderAndMappingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeys_ExpiresAt",
                table: "VirtualKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeys_IsEnabled",
                table: "VirtualKeys",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeys_KeyName",
                table: "VirtualKeys",
                column: "KeyName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VirtualKeys_ExpiresAt",
                table: "VirtualKeys");

            migrationBuilder.DropIndex(
                name: "IX_VirtualKeys_IsEnabled",
                table: "VirtualKeys");

            migrationBuilder.DropIndex(
                name: "IX_VirtualKeys_KeyName",
                table: "VirtualKeys");
        }
    }
}

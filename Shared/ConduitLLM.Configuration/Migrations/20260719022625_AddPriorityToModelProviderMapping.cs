using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityToModelProviderMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ModelProviderMappings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMappings_ModelAlias_IsEnabled_Priority",
                table: "ModelProviderMappings",
                columns: new[] { "ModelAlias", "IsEnabled", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelProviderMappings_ModelAlias_IsEnabled_Priority",
                table: "ModelProviderMappings");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ModelProviderMappings");
        }
    }
}

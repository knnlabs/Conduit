using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RemoveModelTypeAndVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Model_ModelSeriesId_ModelType",
                table: "Models");

            migrationBuilder.DropIndex(
                name: "IX_Model_ModelType",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "ModelType",
                table: "Models");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModelType",
                table: "Models",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Model_ModelSeriesId_ModelType",
                table: "Models",
                columns: new[] { "ModelSeriesId", "ModelType" });

            migrationBuilder.CreateIndex(
                name: "IX_Model_ModelType",
                table: "Models",
                column: "ModelType");
        }
    }
}

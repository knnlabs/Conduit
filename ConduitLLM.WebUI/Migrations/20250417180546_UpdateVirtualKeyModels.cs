using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.WebUI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVirtualKeyModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RateLimitRpd",
                table: "VirtualKeys",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitRpm",
                table: "VirtualKeys",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RateLimitRpd",
                table: "VirtualKeys");

            migrationBuilder.DropColumn(
                name: "RateLimitRpm",
                table: "VirtualKeys");
        }
    }
}

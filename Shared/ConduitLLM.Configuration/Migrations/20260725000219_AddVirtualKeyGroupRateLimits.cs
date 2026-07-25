using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualKeyGroupRateLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxParallelRequests",
                table: "VirtualKeyGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitRpd",
                table: "VirtualKeyGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitRpm",
                table: "VirtualKeyGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitTpm",
                table: "VirtualKeyGroups",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxParallelRequests",
                table: "VirtualKeyGroups");

            migrationBuilder.DropColumn(
                name: "RateLimitRpd",
                table: "VirtualKeyGroups");

            migrationBuilder.DropColumn(
                name: "RateLimitRpm",
                table: "VirtualKeyGroups");

            migrationBuilder.DropColumn(
                name: "RateLimitTpm",
                table: "VirtualKeyGroups");
        }
    }
}

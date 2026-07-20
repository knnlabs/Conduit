using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualKeyIdToIpFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VirtualKeyId",
                table: "IpFilters",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpFilters_VirtualKeyId",
                table: "IpFilters",
                column: "VirtualKeyId");

            migrationBuilder.AddForeignKey(
                name: "FK_IpFilters_VirtualKeys_VirtualKeyId",
                table: "IpFilters",
                column: "VirtualKeyId",
                principalTable: "VirtualKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IpFilters_VirtualKeys_VirtualKeyId",
                table: "IpFilters");

            migrationBuilder.DropIndex(
                name: "IX_IpFilters_VirtualKeyId",
                table: "IpFilters");

            migrationBuilder.DropColumn(
                name: "VirtualKeyId",
                table: "IpFilters");
        }
    }
}

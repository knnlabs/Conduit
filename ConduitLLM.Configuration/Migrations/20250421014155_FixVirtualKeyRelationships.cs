using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class FixVirtualKeyRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_VirtualKeys_VirtualKeyId1",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_VirtualKeySpendHistory_VirtualKeys_VirtualKeyId1",
                table: "VirtualKeySpendHistory");

            migrationBuilder.DropIndex(
                name: "IX_VirtualKeySpendHistory_VirtualKeyId1",
                table: "VirtualKeySpendHistory");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_VirtualKeyId1",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "VirtualKeyId1",
                table: "VirtualKeySpendHistory");

            migrationBuilder.DropColumn(
                name: "VirtualKeyId1",
                table: "Notifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VirtualKeyId1",
                table: "VirtualKeySpendHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VirtualKeyId1",
                table: "Notifications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeySpendHistory_VirtualKeyId1",
                table: "VirtualKeySpendHistory",
                column: "VirtualKeyId1");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_VirtualKeyId1",
                table: "Notifications",
                column: "VirtualKeyId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_VirtualKeys_VirtualKeyId1",
                table: "Notifications",
                column: "VirtualKeyId1",
                principalTable: "VirtualKeys",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VirtualKeySpendHistory_VirtualKeys_VirtualKeyId1",
                table: "VirtualKeySpendHistory",
                column: "VirtualKeyId1",
                principalTable: "VirtualKeys",
                principalColumn: "Id");
        }
    }
}

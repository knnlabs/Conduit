using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderKeyNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderKeyCredentialId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ProviderId",
                table: "Notifications",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ProviderKeyCredentialId",
                table: "Notifications",
                column: "ProviderKeyCredentialId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_ProviderKeyCredentials_ProviderKeyCredentialId",
                table: "Notifications",
                column: "ProviderKeyCredentialId",
                principalTable: "ProviderKeyCredentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Providers_ProviderId",
                table: "Notifications",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_ProviderKeyCredentials_ProviderKeyCredentialId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Providers_ProviderId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ProviderId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ProviderKeyCredentialId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ProviderKeyCredentialId",
                table: "Notifications");
        }
    }
}

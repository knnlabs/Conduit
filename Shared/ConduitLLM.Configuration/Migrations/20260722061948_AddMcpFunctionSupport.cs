using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpFunctionSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_OnePrimaryPerProviderType",
                table: "FunctionCredentials");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_UniqueApiKeyPerProviderType",
                table: "FunctionCredentials");

            migrationBuilder.AlterColumn<string>(
                name: "ApiKey",
                table: "FunctionCredentials",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FunctionConfigurationId",
                table: "FunctionCredentials",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_FunctionConfigurationId",
                table: "FunctionCredentials",
                column: "FunctionConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_OnePrimaryPerConfiguration",
                table: "FunctionCredentials",
                columns: new[] { "FunctionConfigurationId", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true AND \"FunctionConfigurationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_OnePrimaryPerProviderType",
                table: "FunctionCredentials",
                columns: new[] { "ProviderType", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true AND \"FunctionConfigurationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_UniqueApiKeyPerProviderType",
                table: "FunctionCredentials",
                columns: new[] { "ProviderType", "ApiKey" },
                unique: true,
                filter: "\"ApiKey\" IS NOT NULL AND \"FunctionConfigurationId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FunctionCredentials_FunctionConfigurations_FunctionConfigur~",
                table: "FunctionCredentials",
                column: "FunctionConfigurationId",
                principalTable: "FunctionConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FunctionCredentials_FunctionConfigurations_FunctionConfigur~",
                table: "FunctionCredentials");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_FunctionConfigurationId",
                table: "FunctionCredentials");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_OnePrimaryPerConfiguration",
                table: "FunctionCredentials");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_OnePrimaryPerProviderType",
                table: "FunctionCredentials");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_UniqueApiKeyPerProviderType",
                table: "FunctionCredentials");

            migrationBuilder.DropColumn(
                name: "FunctionConfigurationId",
                table: "FunctionCredentials");

            migrationBuilder.AlterColumn<string>(
                name: "ApiKey",
                table: "FunctionCredentials",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_OnePrimaryPerProviderType",
                table: "FunctionCredentials",
                columns: new[] { "ProviderType", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_UniqueApiKeyPerProviderType",
                table: "FunctionCredentials",
                columns: new[] { "ProviderType", "ApiKey" },
                unique: true,
                filter: "\"ApiKey\" IS NOT NULL");
        }
    }
}

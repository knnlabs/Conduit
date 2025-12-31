using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class ProviderBasedFunctionCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new ProviderType column (nullable initially)
            migrationBuilder.AddColumn<int>(
                name: "ProviderType_New",
                table: "FunctionCredentials",
                type: "integer",
                nullable: true);

            // Step 2: Populate ProviderType from associated FunctionConfiguration
            migrationBuilder.Sql(@"
                UPDATE ""FunctionCredentials"" fc
                SET ""ProviderType_New"" = fconfig.""ProviderType""
                FROM ""FunctionConfigurations"" fconfig
                WHERE fc.""FunctionConfigurationId"" = fconfig.""Id""
            ");

            // Step 3: Drop foreign key constraint and old index
            migrationBuilder.DropForeignKey(
                name: "FK_FunctionCredentials_FunctionConfigurations_FunctionConfigur~",
                table: "FunctionCredentials");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_FunctionConfigurationId",
                table: "FunctionCredentials");

            // Step 4: Drop old column
            migrationBuilder.DropColumn(
                name: "FunctionConfigurationId",
                table: "FunctionCredentials");

            // Step 5: Rename new column to final name and make it required
            migrationBuilder.RenameColumn(
                name: "ProviderType_New",
                table: "FunctionCredentials",
                newName: "ProviderType");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderType",
                table: "FunctionCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Step 6: Create new indexes
            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_ProviderType",
                table: "FunctionCredentials",
                column: "ProviderType");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProviderType",
                table: "FunctionCredentials",
                newName: "FunctionConfigurationId");

            migrationBuilder.RenameIndex(
                name: "IX_FunctionCredential_UniqueApiKeyPerProviderType",
                table: "FunctionCredentials",
                newName: "IX_FunctionCredential_UniqueApiKeyPerConfiguration");

            migrationBuilder.RenameIndex(
                name: "IX_FunctionCredential_ProviderType",
                table: "FunctionCredentials",
                newName: "IX_FunctionCredential_FunctionConfigurationId");

            migrationBuilder.RenameIndex(
                name: "IX_FunctionCredential_OnePrimaryPerProviderType",
                table: "FunctionCredentials",
                newName: "IX_FunctionCredential_OnePrimaryPerConfiguration");

            migrationBuilder.AddForeignKey(
                name: "FK_FunctionCredentials_FunctionConfigurations_FunctionConfigur~",
                table: "FunctionCredentials",
                column: "FunctionConfigurationId",
                principalTable: "FunctionConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

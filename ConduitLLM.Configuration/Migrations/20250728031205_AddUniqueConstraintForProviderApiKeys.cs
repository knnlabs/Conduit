using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintForProviderApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeyCredential_UniqueApiKeyPerProvider",
                table: "ProviderKeyCredentials",
                columns: new[] { "ProviderCredentialId", "ApiKey" },
                unique: true,
                filter: "\"ApiKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProviderKeyCredential_UniqueApiKeyPerProvider",
                table: "ProviderKeyCredentials");
        }
    }
}

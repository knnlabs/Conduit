using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <summary>
    /// Contract migration for issue #1244. The preceding expand migrations moved Cloudflare account
    /// IDs into <c>Providers."Settings"</c> and OpenAI organizations into the same provider-scoped
    /// settings bag. Remove the redundant storage after the compatibility window.
    /// </summary>
    public partial class ContractProviderConfigurationStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ProviderType 12 is Cloudflare. Clear only URLs with the exact registered template
            // shape and a canonical account_id setting. Different hosts, schemes, ports, or paths
            // are operator-owned overrides and must survive.
            migrationBuilder.Sql("""
                UPDATE "Providers"
                SET "BaseUrl" = NULL
                WHERE "ProviderType" = 12
                  AND COALESCE("Settings" ->> 'account_id', '') <> ''
                  AND rtrim("BaseUrl", '/') ~*
                      '^https://api[.]cloudflare[.]com/client/v4/accounts/[^/]+/ai/v1$';
                """);

            migrationBuilder.DropColumn(
                name: "Organization",
                table: "ProviderKeyCredentials");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Organization",
                table: "ProviderKeyCredentials",
                type: "text",
                nullable: true);

            // Reconstruct the compatibility values expected by the expand-release model. Per-key
            // organization differences cannot be recovered after contraction, so restore the
            // canonical provider value to each OpenAI key.
            migrationBuilder.Sql("""
                UPDATE "ProviderKeyCredentials" k
                SET "Organization" = p."Settings" ->> 'organization'
                FROM "Providers" p
                WHERE p."Id" = k."ProviderId"
                  AND p."ProviderType" = 1
                  AND COALESCE(p."Settings" ->> 'organization', '') <> ''
                  AND COALESCE(k."Organization", '') = '';

                UPDATE "Providers"
                SET "BaseUrl" = 'https://api.cloudflare.com/client/v4/accounts/'
                                || ("Settings" ->> 'account_id')
                                || '/ai/v1'
                WHERE "ProviderType" = 12
                  AND COALESCE("BaseUrl", '') = ''
                  AND COALESCE("Settings" ->> 'account_id', '') <> '';
                """);
        }
    }
}

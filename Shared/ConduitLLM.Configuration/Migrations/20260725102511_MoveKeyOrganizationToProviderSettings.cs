using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <summary>
    /// Data-only migration for issue #1185. <c>ProviderKeyCredentials."Organization"</c> was
    /// collected and stored but read by no provider client, so the value never reached OpenAI. The
    /// organization is now a header-bound provider setting that is actually sent, so lift whatever
    /// operators entered into <c>Providers."Settings"</c> rather than stranding it.
    /// </summary>
    /// <remarks>
    /// Additive per ADR-002: the source column is left in place for this release and dropped in the
    /// contract step. The organization scopes the account a key belongs to, and settings are
    /// per-provider, so the primary key's value wins; ties among non-primary keys are broken by key
    /// id for determinism. Providers that already carry an <c>organization</c> setting are skipped.
    /// </remarks>
    public partial class MoveKeyOrganizationToProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "ProviderType" = 1 is ProviderType.OpenAI, the only provider that binds an
            // organization header today.
            migrationBuilder.Sql("""
                UPDATE "Providers" p
                SET "Settings" = COALESCE(p."Settings", '{}'::jsonb)
                                 || jsonb_build_object('organization', k."Organization")
                FROM (
                    SELECT DISTINCT ON ("ProviderId") "ProviderId", "Organization"
                    FROM "ProviderKeyCredentials"
                    WHERE COALESCE("Organization", '') <> ''
                    ORDER BY "ProviderId", "IsPrimary" DESC, "Id"
                ) k
                WHERE k."ProviderId" = p."Id"
                  AND p."ProviderType" = 1
                  AND COALESCE(p."Settings" ->> 'organization', '') = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop only the key this migration added: one that still matches a key's stored
            // organization. A value an operator has since edited is left alone.
            migrationBuilder.Sql("""
                UPDATE "Providers" p
                SET "Settings" = NULLIF(p."Settings" - 'organization', '{}'::jsonb)
                WHERE p."ProviderType" = 1
                  AND EXISTS (
                      SELECT 1 FROM "ProviderKeyCredentials" k
                      WHERE k."ProviderId" = p."Id"
                        AND k."Organization" = p."Settings" ->> 'organization');
                """);
        }
    }
}

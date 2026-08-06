using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <summary>
    /// Data-only backfill for issue #1183. Before structured provider settings existed, the only way
    /// to configure Cloudflare Workers AI was to hand-craft the account-scoped base URL, so every
    /// existing install carries its account ID buried inside <c>Providers."BaseUrl"</c>. Lift it into
    /// the <c>Settings</c> bag so the Account ID field is populated in the UI and the identifier is
    /// available structurally.
    /// </summary>
    /// <remarks>
    /// Additive per ADR-002: <c>BaseUrl</c> is left in place, so the previous release's code (which
    /// knows nothing about <c>Settings</c>) resolves exactly the same URL. Dropping the BaseUrl
    /// reliance is the contract step and ships a later release. Rows whose base URL points somewhere
    /// other than Cloudflare's account-scoped API (a proxy, for example) carry no account ID to
    /// recover and are left untouched.
    /// </remarks>
    public partial class BackfillCloudflareAccountIdSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "ProviderType" = 12 is ProviderType.Cloudflare; enum values are stable identifiers
            // persisted as integers.
            migrationBuilder.Sql("""
                UPDATE "Providers"
                SET "Settings" = COALESCE("Settings", '{}'::jsonb) || jsonb_build_object(
                        'account_id',
                        substring("BaseUrl" from '/accounts/([0-9a-fA-F]{32})(?:/|$)'))
                WHERE "ProviderType" = 12
                  AND "BaseUrl" ~ '/accounts/[0-9a-fA-F]{32}(/|$)'
                  AND COALESCE("Settings" ->> 'account_id', '') = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop only the key this migration added: one that still matches the account ID embedded
            // in the base URL. An account ID an operator has since edited is left alone.
            migrationBuilder.Sql("""
                UPDATE "Providers"
                SET "Settings" = NULLIF("Settings" - 'account_id', '{}'::jsonb)
                WHERE "ProviderType" = 12
                  AND "BaseUrl" ~ '/accounts/[0-9a-fA-F]{32}(/|$)'
                  AND "Settings" ->> 'account_id'
                      = substring("BaseUrl" from '/accounts/([0-9a-fA-F]{32})(?:/|$)');
                """);
        }
    }
}

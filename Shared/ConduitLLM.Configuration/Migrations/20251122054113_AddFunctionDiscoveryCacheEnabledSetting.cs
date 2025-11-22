using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionDiscoveryCacheEnabledSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert the Functions.DiscoveryCacheEnabled global setting (default: disabled)
            migrationBuilder.Sql(@"
                INSERT INTO ""GlobalSettings"" (""Key"", ""Value"", ""Description"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    'Functions.DiscoveryCacheEnabled',
                    'false',
                    'Enable caching of function discovery results to improve performance. When enabled, function tool definitions are cached according to each function''s CacheTtlMinutes setting.',
                    NOW(),
                    NOW()
                )
                ON CONFLICT (""Key"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the global setting
            migrationBuilder.Sql(@"
                DELETE FROM ""GlobalSettings""
                WHERE ""Key"" = 'Functions.DiscoveryCacheEnabled';
            ");
        }
    }
}

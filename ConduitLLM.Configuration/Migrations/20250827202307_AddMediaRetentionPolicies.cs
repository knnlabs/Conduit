using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaRetentionPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only drop if the table exists (for fresh installs it won't)
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MediaLifecycleRecords"" CASCADE;");

            migrationBuilder.AddColumn<int>(
                name: "MediaRetentionPolicyId",
                table: "VirtualKeyGroups",
                type: "integer",
                nullable: true);

            // Only modify columns if the table exists
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BillingAuditEvents') THEN
                        ALTER TABLE ""BillingAuditEvents"" 
                        ALTER COLUMN ""Timestamp"" SET DEFAULT CURRENT_TIMESTAMP;
                        
                        ALTER TABLE ""BillingAuditEvents"" 
                        ALTER COLUMN ""Id"" SET DEFAULT gen_random_uuid();
                    END IF;
                END $$;
            ");

            migrationBuilder.CreateTable(
                name: "MediaRetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PositiveBalanceRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    ZeroBalanceRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    NegativeBalanceRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    SoftDeleteGracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    RespectRecentAccess = table.Column<bool>(type: "boolean", nullable: false),
                    RecentAccessWindowDays = table.Column<int>(type: "integer", nullable: false),
                    IsProTier = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    MaxStorageSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    MaxFileCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRetentionPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroups_MediaRetentionPolicyId",
                table: "VirtualKeyGroups",
                column: "MediaRetentionPolicyId");

            // Only create indexes if the table exists
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BillingAuditEvents') THEN
                        CREATE INDEX IF NOT EXISTS ""IX_BillingAuditEvents_EventType"" ON ""BillingAuditEvents"" (""EventType"");
                        CREATE INDEX IF NOT EXISTS ""IX_BillingAuditEvents_EventType_Timestamp"" ON ""BillingAuditEvents"" (""EventType"", ""Timestamp"");
                        CREATE INDEX IF NOT EXISTS ""IX_BillingAuditEvents_RequestId"" ON ""BillingAuditEvents"" (""RequestId"");
                        CREATE INDEX IF NOT EXISTS ""IX_BillingAuditEvents_Timestamp"" ON ""BillingAuditEvents"" (""Timestamp"");
                        CREATE INDEX IF NOT EXISTS ""IX_BillingAuditEvents_VirtualKeyId_Timestamp"" ON ""BillingAuditEvents"" (""VirtualKeyId"", ""Timestamp"");
                    END IF;
                END $$;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRetentionPolicies_IsActive",
                table: "MediaRetentionPolicies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRetentionPolicies_IsDefault",
                table: "MediaRetentionPolicies",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRetentionPolicies_Name",
                table: "MediaRetentionPolicies",
                column: "Name",
                unique: true);

            // Only add foreign key if the table exists
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BillingAuditEvents') THEN
                        ALTER TABLE ""BillingAuditEvents"" 
                        ADD CONSTRAINT ""FK_BillingAuditEvents_VirtualKeys_VirtualKeyId"" 
                        FOREIGN KEY (""VirtualKeyId"") REFERENCES ""VirtualKeys"" (""Id"") ON DELETE SET NULL;
                    END IF;
                EXCEPTION WHEN duplicate_object THEN
                    NULL; -- Ignore if constraint already exists
                END $$;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_VirtualKeyGroups_MediaRetentionPolicies_MediaRetentionPolic~",
                table: "VirtualKeyGroups",
                column: "MediaRetentionPolicyId",
                principalTable: "MediaRetentionPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Insert default retention policy
            migrationBuilder.Sql(@"
                INSERT INTO ""MediaRetentionPolicies"" (
                    ""Id"",
                    ""Name"",
                    ""Description"",
                    ""PositiveBalanceRetentionDays"",
                    ""ZeroBalanceRetentionDays"",
                    ""NegativeBalanceRetentionDays"",
                    ""SoftDeleteGracePeriodDays"",
                    ""RespectRecentAccess"",
                    ""RecentAccessWindowDays"",
                    ""IsProTier"",
                    ""IsDefault"",
                    ""MaxStorageSizeBytes"",
                    ""MaxFileCount"",
                    ""CreatedAt"",
                    ""UpdatedAt"",
                    ""IsActive""
                )
                SELECT 
                    1,
                    'Default',
                    'Standard retention policy for all virtual key groups',
                    60,  -- 60 days for positive balance
                    14,  -- 14 days for zero balance
                    3,   -- 3 days for negative balance
                    7,   -- 7 days soft delete grace period
                    true, -- Respect recent access
                    7,   -- 7 days recent access window
                    false, -- Not pro tier
                    true,  -- Is default policy
                    NULL,  -- No storage limit
                    NULL,  -- No file count limit
                    NOW(),
                    NOW(),
                    true
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""MediaRetentionPolicies"" WHERE ""IsDefault"" = true
                )
            ");

            // Insert pro tier retention policy
            migrationBuilder.Sql(@"
                INSERT INTO ""MediaRetentionPolicies"" (
                    ""Id"",
                    ""Name"",
                    ""Description"",
                    ""PositiveBalanceRetentionDays"",
                    ""ZeroBalanceRetentionDays"",
                    ""NegativeBalanceRetentionDays"",
                    ""SoftDeleteGracePeriodDays"",
                    ""RespectRecentAccess"",
                    ""RecentAccessWindowDays"",
                    ""IsProTier"",
                    ""IsDefault"",
                    ""MaxStorageSizeBytes"",
                    ""MaxFileCount"",
                    ""CreatedAt"",
                    ""UpdatedAt"",
                    ""IsActive""
                )
                SELECT 
                    2,
                    'Pro Tier',
                    'Extended retention policy for pro tier customers',
                    180, -- 180 days for positive balance
                    30,  -- 30 days for zero balance
                    7,   -- 7 days for negative balance
                    14,  -- 14 days soft delete grace period
                    true, -- Respect recent access
                    14,  -- 14 days recent access window
                    true, -- Is pro tier
                    false, -- Not default
                    10737418240, -- 10GB storage limit
                    10000, -- 10,000 file limit
                    NOW(),
                    NOW(),
                    true
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""MediaRetentionPolicies"" WHERE ""Name"" = 'Pro Tier'
                )
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillingAuditEvents_VirtualKeys_VirtualKeyId",
                table: "BillingAuditEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_VirtualKeyGroups_MediaRetentionPolicies_MediaRetentionPolic~",
                table: "VirtualKeyGroups");

            migrationBuilder.DropTable(
                name: "MediaRetentionPolicies");

            migrationBuilder.DropIndex(
                name: "IX_VirtualKeyGroups_MediaRetentionPolicyId",
                table: "VirtualKeyGroups");

            migrationBuilder.DropIndex(
                name: "IX_BillingAuditEvents_EventType",
                table: "BillingAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_BillingAuditEvents_EventType_Timestamp",
                table: "BillingAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_BillingAuditEvents_RequestId",
                table: "BillingAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_BillingAuditEvents_Timestamp",
                table: "BillingAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_BillingAuditEvents_VirtualKeyId_Timestamp",
                table: "BillingAuditEvents");

            migrationBuilder.DropColumn(
                name: "MediaRetentionPolicyId",
                table: "VirtualKeyGroups");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "BillingAuditEvents",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "BillingAuditEvents",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateTable(
                name: "MediaLifecycleRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedByModel = table.Column<string>(type: "text", nullable: false),
                    GenerationPrompt = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MediaType = table.Column<string>(type: "text", nullable: false),
                    MediaUrl = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaLifecycleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaLifecycleRecords_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_CreatedAt",
                table: "MediaLifecycleRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_ExpiresAt",
                table: "MediaLifecycleRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_ExpiresAt_IsDeleted",
                table: "MediaLifecycleRecords",
                columns: new[] { "ExpiresAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_StorageKey",
                table: "MediaLifecycleRecords",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_VirtualKeyId",
                table: "MediaLifecycleRecords",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_VirtualKeyId_IsDeleted",
                table: "MediaLifecycleRecords",
                columns: new[] { "VirtualKeyId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_BillingAuditEvents_VirtualKeys_VirtualKeyId",
                table: "BillingAuditEvents",
                column: "VirtualKeyId",
                principalTable: "VirtualKeys",
                principalColumn: "Id");
        }
    }
}

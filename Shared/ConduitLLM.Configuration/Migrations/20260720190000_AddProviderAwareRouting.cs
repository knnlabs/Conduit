using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations;

public partial class AddProviderAwareRouting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("RoutingPriority", "ModelProviderMappings", nullable: false, defaultValue: 100);
        migrationBuilder.AddColumn<decimal>("RoutingWeight", "ModelProviderMappings", "decimal(4,2)", nullable: false, defaultValue: 1.0m);

        migrationBuilder.CreateTable(
            "ModelRoutePolicies",
            table => new
            {
                Id = table.Column<int>(nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ModelAlias = table.Column<string>(maxLength: 100, nullable: false),
                Strategy = table.Column<string>(maxLength: 30, nullable: false, defaultValue: "Balanced"),
                CostWeight = table.Column<decimal>("decimal(4,3)", nullable: false, defaultValue: 0.40m),
                SpeedWeight = table.Column<decimal>("decimal(4,3)", nullable: false, defaultValue: 0.30m),
                QualityWeight = table.Column<decimal>("decimal(4,3)", nullable: false, defaultValue: 0.30m),
                CacheAffinityEnabled = table.Column<bool>(nullable: false, defaultValue: true),
                AffinityTtlSeconds = table.Column<int>(nullable: false, defaultValue: 1800),
                MaxAffinityScorePenalty = table.Column<decimal>("decimal(4,3)", nullable: false, defaultValue: 0.10m),
                IsEnabled = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelRoutePolicies", x => x.Id);
                table.CheckConstraint(
                    "CK_ModelRoutePolicy_Affinity",
                    "\"AffinityTtlSeconds\" > 0 AND \"MaxAffinityScorePenalty\" >= 0");
                table.CheckConstraint(
                    "CK_ModelRoutePolicy_Weights",
                    "\"CostWeight\" >= 0 AND \"SpeedWeight\" >= 0 AND \"QualityWeight\" >= 0");
            });
        migrationBuilder.CreateIndex("IX_ModelRoutePolicies_ModelAlias", "ModelRoutePolicies", "ModelAlias", unique: true);
        migrationBuilder.Sql("""
            INSERT INTO "ModelRoutePolicies" ("ModelAlias", "Strategy", "CostWeight", "SpeedWeight", "QualityWeight", "CacheAffinityEnabled", "AffinityTtlSeconds", "MaxAffinityScorePenalty", "IsEnabled", "CreatedAt", "UpdatedAt")
            SELECT DISTINCT "ModelAlias", 'Balanced', 0.40, 0.30, 0.30, TRUE, 1800, 0.10, TRUE, NOW(), NOW()
            FROM "ModelProviderMappings";
            """);
        migrationBuilder.Sql("""
            INSERT INTO "GlobalSettings" ("Key", "Value", "Description", "CreatedAt", "UpdatedAt")
            VALUES
              ('Routing.Chat.Enabled', 'true', 'Emergency provider-aware chat routing switch', NOW(), NOW()),
              ('Routing.Defaults', '{"chatRoutingEnabled":true,"strategy":"Balanced","costWeight":0.40,"speedWeight":0.30,"qualityWeight":0.30,"cacheAffinityEnabled":true,"affinityTtlSeconds":1800,"maxAffinityScorePenalty":0.10,"mappingPriority":100,"mappingWeight":1.0}', 'Default provider-aware chat routing policy', NOW(), NOW())
            ON CONFLICT ("Key") DO NOTHING;
            """);

        migrationBuilder.AddColumn<int>("ModelProviderMappingId", "RequestLogs", nullable: true);
        migrationBuilder.AddColumn<bool>("PromptCachingEligible", "RequestLogs", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("PromptCachingPolicyApplied", "RequestLogs", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<decimal>("CachedReadSavings", "RequestLogs", "decimal(18,8)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("CacheWritePremium", "RequestLogs", "decimal(18,8)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<bool>("RoutingAffinityUsed", "RequestLogs", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("RoutingDecisionReason", "RequestLogs", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<int>("RoutingFailoverCount", "RequestLogs", nullable: false, defaultValue: 0);
        migrationBuilder.CreateIndex("IX_RequestLogs_ModelProviderMappingId_Timestamp", "RequestLogs", new[] { "ModelProviderMappingId", "Timestamp" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ModelRoutePolicies");
        migrationBuilder.DropIndex("IX_RequestLogs_ModelProviderMappingId_Timestamp", "RequestLogs");
        migrationBuilder.DropColumn("RoutingPriority", "ModelProviderMappings");
        migrationBuilder.DropColumn("RoutingWeight", "ModelProviderMappings");
        migrationBuilder.DropColumn("ModelProviderMappingId", "RequestLogs");
        migrationBuilder.DropColumn("PromptCachingEligible", "RequestLogs");
        migrationBuilder.DropColumn("PromptCachingPolicyApplied", "RequestLogs");
        migrationBuilder.DropColumn("CachedReadSavings", "RequestLogs");
        migrationBuilder.DropColumn("CacheWritePremium", "RequestLogs");
        migrationBuilder.DropColumn("RoutingAffinityUsed", "RequestLogs");
        migrationBuilder.DropColumn("RoutingDecisionReason", "RequestLogs");
        migrationBuilder.DropColumn("RoutingFailoverCount", "RequestLogs");
    }
}

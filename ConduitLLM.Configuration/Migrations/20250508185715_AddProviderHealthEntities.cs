using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderHealthEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FallbackConfigurations_RouterConfigurations_RouterConfigId",
                table: "FallbackConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelDeployments_RouterConfigurations_RouterConfigId",
                table: "ModelDeployments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RouterConfigurations",
                table: "RouterConfigurations");

            migrationBuilder.RenameTable(
                name: "RouterConfigurations",
                newName: "RouterConfigEntity");

            migrationBuilder.RenameIndex(
                name: "IX_RouterConfigurations_LastUpdated",
                table: "RouterConfigEntity",
                newName: "IX_RouterConfigEntity_LastUpdated");

            migrationBuilder.AddColumn<DateTime>(
                name: "Timestamp",
                table: "VirtualKeySpendHistory",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ModelDeployments",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DeploymentName",
                table: "ModelDeployments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ModelDeployments",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "FallbackModelMappings",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "SourceModelName",
                table: "FallbackModelMappings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "FallbackModelMappings",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "FallbackConfigurations",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "FallbackConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "FallbackConfigurations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "FallbackConfigurations",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RouterConfigEntity",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "RouterConfigEntity",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "RouterConfigEntity",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RouterConfigEntity",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_RouterConfigEntity",
                table: "RouterConfigEntity",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ProviderHealthConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderName = table.Column<string>(type: "TEXT", nullable: false),
                    MonitoringEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CheckIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ConsecutiveFailuresThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CustomEndpointUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    LastCheckedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderHealthConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHealthRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderName = table.Column<string>(type: "TEXT", nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    StatusMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResponseTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    ErrorCategory = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ErrorDetails = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    EndpointUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderHealthRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHealthConfigurations_ProviderName",
                table: "ProviderHealthConfigurations",
                column: "ProviderName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHealthRecords_IsOnline",
                table: "ProviderHealthRecords",
                column: "IsOnline");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHealthRecords_ProviderName_TimestampUtc",
                table: "ProviderHealthRecords",
                columns: new[] { "ProviderName", "TimestampUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_FallbackConfigurations_RouterConfigEntity_RouterConfigId",
                table: "FallbackConfigurations",
                column: "RouterConfigId",
                principalTable: "RouterConfigEntity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelDeployments_RouterConfigEntity_RouterConfigId",
                table: "ModelDeployments",
                column: "RouterConfigId",
                principalTable: "RouterConfigEntity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FallbackConfigurations_RouterConfigEntity_RouterConfigId",
                table: "FallbackConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelDeployments_RouterConfigEntity_RouterConfigId",
                table: "ModelDeployments");

            migrationBuilder.DropTable(
                name: "ProviderHealthConfigurations");

            migrationBuilder.DropTable(
                name: "ProviderHealthRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RouterConfigEntity",
                table: "RouterConfigEntity");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "VirtualKeySpendHistory");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ModelDeployments");

            migrationBuilder.DropColumn(
                name: "DeploymentName",
                table: "ModelDeployments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ModelDeployments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "FallbackModelMappings");

            migrationBuilder.DropColumn(
                name: "SourceModelName",
                table: "FallbackModelMappings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FallbackModelMappings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "FallbackConfigurations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "FallbackConfigurations");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "FallbackConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FallbackConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RouterConfigEntity");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "RouterConfigEntity");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "RouterConfigEntity");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RouterConfigEntity");

            migrationBuilder.RenameTable(
                name: "RouterConfigEntity",
                newName: "RouterConfigurations");

            migrationBuilder.RenameIndex(
                name: "IX_RouterConfigEntity_LastUpdated",
                table: "RouterConfigurations",
                newName: "IX_RouterConfigurations_LastUpdated");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RouterConfigurations",
                table: "RouterConfigurations",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FallbackConfigurations_RouterConfigurations_RouterConfigId",
                table: "FallbackConfigurations",
                column: "RouterConfigId",
                principalTable: "RouterConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelDeployments_RouterConfigurations_RouterConfigId",
                table: "ModelDeployments",
                column: "RouterConfigId",
                principalTable: "RouterConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

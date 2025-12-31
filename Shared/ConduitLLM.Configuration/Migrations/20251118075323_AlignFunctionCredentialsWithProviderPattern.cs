using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AlignFunctionCredentialsWithProviderPattern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FunctionExecutionAudits_FunctionExecutionId",
                table: "FunctionExecutionAudits");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCostMappings_FunctionConfigurationId",
                table: "FunctionCostMappings");

            migrationBuilder.RenameColumn(
                name: "CredentialName",
                table: "FunctionCredentials",
                newName: "KeyName");

            migrationBuilder.RenameColumn(
                name: "CredentialGroup",
                table: "FunctionCredentials",
                newName: "FunctionAccountGroup");

            migrationBuilder.RenameIndex(
                name: "IX_FunctionCredentials_FunctionConfigurationId",
                table: "FunctionCredentials",
                newName: "IX_FunctionCredential_FunctionConfigurationId");

            migrationBuilder.AddColumn<Guid>(
                name: "FunctionExecutionId1",
                table: "FunctionExecutionAudits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FunctionCostId1",
                table: "FunctionCostMappings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecution_AsyncProcessing",
                table: "FunctionExecutions",
                columns: new[] { "State", "NextRetryAt", "LeasedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecution_RequestedAt",
                table: "FunctionExecutions",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecution_State",
                table: "FunctionExecutions",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecution_VirtualKeyId",
                table: "FunctionExecutions",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecutionAudit_ExecutionTimestamp",
                table: "FunctionExecutionAudits",
                columns: new[] { "FunctionExecutionId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecutionAudits_FunctionExecutionId1",
                table: "FunctionExecutionAudits",
                column: "FunctionExecutionId1");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_OnePrimaryPerConfiguration",
                table: "FunctionCredentials",
                columns: new[] { "FunctionConfigurationId", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCredential_UniqueApiKeyPerConfiguration",
                table: "FunctionCredentials",
                columns: new[] { "FunctionConfigurationId", "ApiKey" },
                unique: true,
                filter: "\"ApiKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FunctionCredential_AccountGroupRange",
                table: "FunctionCredentials",
                sql: "\"FunctionAccountGroup\" >= 0 AND \"FunctionAccountGroup\" <= 32");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FunctionCredential_PrimaryMustBeEnabled",
                table: "FunctionCredentials",
                sql: "\"IsPrimary\" = false OR \"IsEnabled\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCost_EffectiveDates",
                table: "FunctionCosts",
                columns: new[] { "EffectiveDate", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCost_IsActive",
                table: "FunctionCosts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCostMapping_Unique",
                table: "FunctionCostMappings",
                columns: new[] { "FunctionConfigurationId", "FunctionCostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCostMappings_FunctionCostId1",
                table: "FunctionCostMappings",
                column: "FunctionCostId1");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionConfiguration_IsEnabled",
                table: "FunctionConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionConfiguration_ProviderType",
                table: "FunctionConfigurations",
                column: "ProviderType");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionConfiguration_Purpose",
                table: "FunctionConfigurations",
                column: "Purpose");

            migrationBuilder.AddForeignKey(
                name: "FK_FunctionCostMappings_FunctionCosts_FunctionCostId1",
                table: "FunctionCostMappings",
                column: "FunctionCostId1",
                principalTable: "FunctionCosts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FunctionExecutionAudits_FunctionExecutions_FunctionExecuti~1",
                table: "FunctionExecutionAudits",
                column: "FunctionExecutionId1",
                principalTable: "FunctionExecutions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FunctionCostMappings_FunctionCosts_FunctionCostId1",
                table: "FunctionCostMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_FunctionExecutionAudits_FunctionExecutions_FunctionExecuti~1",
                table: "FunctionExecutionAudits");

            migrationBuilder.DropIndex(
                name: "IX_FunctionExecution_AsyncProcessing",
                table: "FunctionExecutions");

            migrationBuilder.DropIndex(
                name: "IX_FunctionExecution_RequestedAt",
                table: "FunctionExecutions");

            migrationBuilder.DropIndex(
                name: "IX_FunctionExecution_State",
                table: "FunctionExecutions");

            migrationBuilder.DropIndex(
                name: "IX_FunctionExecution_VirtualKeyId",
                table: "FunctionExecutions");

            migrationBuilder.DropIndex(
                name: "IX_FunctionExecutionAudit_ExecutionTimestamp",
                table: "FunctionExecutionAudits");

            migrationBuilder.DropIndex(
                name: "IX_FunctionExecutionAudits_FunctionExecutionId1",
                table: "FunctionExecutionAudits");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_OnePrimaryPerConfiguration",
                table: "FunctionCredentials");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCredential_UniqueApiKeyPerConfiguration",
                table: "FunctionCredentials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FunctionCredential_AccountGroupRange",
                table: "FunctionCredentials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FunctionCredential_PrimaryMustBeEnabled",
                table: "FunctionCredentials");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCost_EffectiveDates",
                table: "FunctionCosts");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCost_IsActive",
                table: "FunctionCosts");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCostMapping_Unique",
                table: "FunctionCostMappings");

            migrationBuilder.DropIndex(
                name: "IX_FunctionCostMappings_FunctionCostId1",
                table: "FunctionCostMappings");

            migrationBuilder.DropIndex(
                name: "IX_FunctionConfiguration_IsEnabled",
                table: "FunctionConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_FunctionConfiguration_ProviderType",
                table: "FunctionConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_FunctionConfiguration_Purpose",
                table: "FunctionConfigurations");

            migrationBuilder.DropColumn(
                name: "FunctionExecutionId1",
                table: "FunctionExecutionAudits");

            migrationBuilder.DropColumn(
                name: "FunctionCostId1",
                table: "FunctionCostMappings");

            migrationBuilder.RenameColumn(
                name: "KeyName",
                table: "FunctionCredentials",
                newName: "CredentialName");

            migrationBuilder.RenameColumn(
                name: "FunctionAccountGroup",
                table: "FunctionCredentials",
                newName: "CredentialGroup");

            migrationBuilder.RenameIndex(
                name: "IX_FunctionCredential_FunctionConfigurationId",
                table: "FunctionCredentials",
                newName: "IX_FunctionCredentials_FunctionConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionExecutionAudits_FunctionExecutionId",
                table: "FunctionExecutionAudits",
                column: "FunctionExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCostMappings_FunctionConfigurationId",
                table: "FunctionCostMappings",
                column: "FunctionConfigurationId");
        }
    }
}

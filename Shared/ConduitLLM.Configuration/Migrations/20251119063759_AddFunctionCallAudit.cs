using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionCallAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FunctionCallAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatCompletionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FunctionExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FunctionConfigurationId = table.Column<int>(type: "integer", nullable: false),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    FunctionCallJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IterationNumber = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionCallAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FunctionCallAudits_FunctionConfigurations_FunctionConfigura~",
                        column: x => x.FunctionConfigurationId,
                        principalTable: "FunctionConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FunctionCallAudits_FunctionExecutions_FunctionExecutionId",
                        column: x => x.FunctionExecutionId,
                        principalTable: "FunctionExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCallAudit_ChatCompletionId",
                table: "FunctionCallAudits",
                column: "ChatCompletionId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCallAudit_FunctionConfigurationId",
                table: "FunctionCallAudits",
                column: "FunctionConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCallAudit_FunctionExecutionId",
                table: "FunctionCallAudits",
                column: "FunctionExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCallAudit_RequestId",
                table: "FunctionCallAudits",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCallAudit_TimestampEventType",
                table: "FunctionCallAudits",
                columns: new[] { "Timestamp", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCallAudit_VirtualKeyId",
                table: "FunctionCallAudits",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionCallAudit_VirtualKeyTimestamp",
                table: "FunctionCallAudits",
                columns: new[] { "VirtualKeyId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FunctionCallAudits");
        }
    }
}

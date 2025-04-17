using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.WebUI.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VirtualKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AllowedModels = table.Column<string>(type: "TEXT", nullable: true),
                    MaxBudget = table.Column<decimal>(type: "decimal(18, 8)", nullable: true),
                    CurrentSpend = table.Column<decimal>(type: "decimal(18, 8)", nullable: false),
                    BudgetDuration = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BudgetStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeys_KeyHash",
                table: "VirtualKeys",
                column: "KeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VirtualKeys");
        }
    }
}

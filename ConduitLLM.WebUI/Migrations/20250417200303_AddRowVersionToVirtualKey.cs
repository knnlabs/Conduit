using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.WebUI.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionToVirtualKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "VirtualKeys",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelIdPattern = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    InputTokenCost = table.Column<decimal>(type: "decimal(18, 10)", nullable: false),
                    OutputTokenCost = table.Column<decimal>(type: "decimal(18, 10)", nullable: false),
                    EmbeddingTokenCost = table.Column<decimal>(type: "decimal(18, 10)", nullable: true),
                    ImageCostPerImage = table.Column<decimal>(type: "decimal(18, 4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCosts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCosts_ModelIdPattern",
                table: "ModelCosts",
                column: "ModelIdPattern");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelCosts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "VirtualKeys");
        }
    }
}

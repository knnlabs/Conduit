using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualKeyGroupTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VirtualKeyGroupTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VirtualKeyGroupId = table.Column<int>(type: "integer", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    ReferenceType = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InitiatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InitiatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeyGroupTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualKeyGroupTransactions_VirtualKeyGroups_VirtualKeyGrou~",
                        column: x => x.VirtualKeyGroupId,
                        principalTable: "VirtualKeyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_CreatedAt",
                table: "VirtualKeyGroupTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_IsDeleted_CreatedAt",
                table: "VirtualKeyGroupTransactions",
                columns: new[] { "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_ReferenceType",
                table: "VirtualKeyGroupTransactions",
                column: "ReferenceType");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_TransactionType",
                table: "VirtualKeyGroupTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_VirtualKeyGroupId",
                table: "VirtualKeyGroupTransactions",
                column: "VirtualKeyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_VirtualKeyGroupId_CreatedAt",
                table: "VirtualKeyGroupTransactions",
                columns: new[] { "VirtualKeyGroupId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VirtualKeyGroupTransactions");
        }
    }
}

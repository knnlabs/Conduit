using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderKeyCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProviderType",
                table: "ProviderCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProviderKeyCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderCredentialId = table.Column<int>(type: "integer", nullable: false),
                    ProviderAccountGroup = table.Column<short>(type: "smallint", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    BaseUrl = table.Column<string>(type: "text", nullable: true),
                    ApiVersion = table.Column<string>(type: "text", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderKeyCredentials", x => x.Id);
                    table.CheckConstraint("CK_ProviderKeyCredential_AccountGroupRange", "\"ProviderAccountGroup\" >= 0 AND \"ProviderAccountGroup\" <= 32");
                    table.CheckConstraint("CK_ProviderKeyCredential_PrimaryMustBeEnabled", "\"IsPrimary\" = false OR \"IsEnabled\" = true");
                    table.ForeignKey(
                        name: "FK_ProviderKeyCredentials_ProviderCredentials_ProviderCredenti~",
                        column: x => x.ProviderCredentialId,
                        principalTable: "ProviderCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeyCredential_OnePrimaryPerProvider",
                table: "ProviderKeyCredentials",
                columns: new[] { "ProviderCredentialId", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeyCredential_ProviderCredentialId",
                table: "ProviderKeyCredentials",
                column: "ProviderCredentialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderKeyCredentials");

            migrationBuilder.DropColumn(
                name: "ProviderType",
                table: "ProviderCredentials");
        }
    }
}

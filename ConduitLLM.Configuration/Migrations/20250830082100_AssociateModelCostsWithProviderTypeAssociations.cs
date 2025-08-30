using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AssociateModelCostsWithProviderTypeAssociations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelCostMappings_ModelCosts_ModelCostId",
                table: "ModelCostMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelCostMappings_ModelProviderMappings_ModelProviderMappin~",
                table: "ModelCostMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelIdentifiers_ModelCosts_ModelCostId",
                table: "ModelIdentifiers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ModelCostMappings",
                table: "ModelCostMappings");

            migrationBuilder.DropIndex(
                name: "IX_ModelCostMappings_ModelCostId_ModelProviderMappingId",
                table: "ModelCostMappings");

            migrationBuilder.RenameTable(
                name: "ModelCostMappings",
                newName: "ModelCostMapping");

            migrationBuilder.RenameIndex(
                name: "IX_ModelCostMappings_ModelProviderMappingId",
                table: "ModelCostMapping",
                newName: "IX_ModelCostMapping_ModelProviderMappingId");

            migrationBuilder.AlterColumn<int>(
                name: "ModelProviderMappingId",
                table: "ModelCostMapping",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ModelProviderTypeAssociationId",
                table: "ModelCostMapping",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ModelCostMapping",
                table: "ModelCostMapping",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifiers_ModelId_Identifier_Provider",
                table: "ModelIdentifiers",
                columns: new[] { "ModelId", "Identifier", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelCostMapping_ModelCostId",
                table: "ModelCostMapping",
                column: "ModelCostId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCostMapping_ModelProviderTypeAssociationId",
                table: "ModelCostMapping",
                column: "ModelProviderTypeAssociationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelCostMapping_ModelCosts_ModelCostId",
                table: "ModelCostMapping",
                column: "ModelCostId",
                principalTable: "ModelCosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelCostMapping_ModelIdentifiers_ModelProviderTypeAssociat~",
                table: "ModelCostMapping",
                column: "ModelProviderTypeAssociationId",
                principalTable: "ModelIdentifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelCostMapping_ModelProviderMappings_ModelProviderMapping~",
                table: "ModelCostMapping",
                column: "ModelProviderMappingId",
                principalTable: "ModelProviderMappings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelIdentifiers_ModelCosts_ModelCostId",
                table: "ModelIdentifiers",
                column: "ModelCostId",
                principalTable: "ModelCosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelCostMapping_ModelCosts_ModelCostId",
                table: "ModelCostMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelCostMapping_ModelIdentifiers_ModelProviderTypeAssociat~",
                table: "ModelCostMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelCostMapping_ModelProviderMappings_ModelProviderMapping~",
                table: "ModelCostMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelIdentifiers_ModelCosts_ModelCostId",
                table: "ModelIdentifiers");

            migrationBuilder.DropIndex(
                name: "IX_ModelIdentifiers_ModelId_Identifier_Provider",
                table: "ModelIdentifiers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ModelCostMapping",
                table: "ModelCostMapping");

            migrationBuilder.DropIndex(
                name: "IX_ModelCostMapping_ModelCostId",
                table: "ModelCostMapping");

            migrationBuilder.DropIndex(
                name: "IX_ModelCostMapping_ModelProviderTypeAssociationId",
                table: "ModelCostMapping");

            migrationBuilder.DropColumn(
                name: "ModelProviderTypeAssociationId",
                table: "ModelCostMapping");

            migrationBuilder.RenameTable(
                name: "ModelCostMapping",
                newName: "ModelCostMappings");

            migrationBuilder.RenameIndex(
                name: "IX_ModelCostMapping_ModelProviderMappingId",
                table: "ModelCostMappings",
                newName: "IX_ModelCostMappings_ModelProviderMappingId");

            migrationBuilder.AlterColumn<int>(
                name: "ModelProviderMappingId",
                table: "ModelCostMappings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ModelCostMappings",
                table: "ModelCostMappings",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCostMappings_ModelCostId_ModelProviderMappingId",
                table: "ModelCostMappings",
                columns: new[] { "ModelCostId", "ModelProviderMappingId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelCostMappings_ModelCosts_ModelCostId",
                table: "ModelCostMappings",
                column: "ModelCostId",
                principalTable: "ModelCosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelCostMappings_ModelProviderMappings_ModelProviderMappin~",
                table: "ModelCostMappings",
                column: "ModelProviderMappingId",
                principalTable: "ModelProviderMappings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelIdentifiers_ModelCosts_ModelCostId",
                table: "ModelIdentifiers",
                column: "ModelCostId",
                principalTable: "ModelCosts",
                principalColumn: "Id");
        }
    }
}

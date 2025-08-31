using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class CompleteModelIdRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if foreign key exists before dropping
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ModelProviderMappings_Models_ModelId') THEN
                        ALTER TABLE ""ModelProviderMappings"" DROP CONSTRAINT ""FK_ModelProviderMappings_Models_ModelId"";
                    END IF;
                END $$;
            ");

            // Check if index exists before dropping
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_ModelProviderMapping_ModelId"";
            ");

            // Check if column exists before dropping
            migrationBuilder.Sql(@"
                ALTER TABLE ""ModelProviderMappings"" DROP COLUMN IF EXISTS ""ModelId"";
            ");

            migrationBuilder.AlterColumn<int>(
                name: "ModelProviderTypeAssociationId",
                table: "ModelProviderMappings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
                
            // Check if index exists before creating
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ModelProviderMapping_ModelProviderTypeAssociationId""
                ON ""ModelProviderMappings"" (""ModelProviderTypeAssociationId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelProviderMapping_ModelProviderTypeAssociationId",
                table: "ModelProviderMappings");
                
            migrationBuilder.AlterColumn<int>(
                name: "ModelProviderTypeAssociationId",
                table: "ModelProviderMappings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ModelId",
                table: "ModelProviderMappings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
                
            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMapping_ModelId",
                table: "ModelProviderMappings",
                column: "ModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelProviderMappings_Models_ModelId",
                table: "ModelProviderMappings",
                column: "ModelId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

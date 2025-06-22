using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddImageGenerationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use proper boolean type based on the database provider
            var columnType = migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL" 
                ? "boolean" 
                : "INTEGER";
                
            migrationBuilder.AddColumn<bool>(
                name: "SupportsImageGeneration",
                table: "ModelProviderMappings",
                type: columnType,
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportsImageGeneration",
                table: "ModelProviderMappings");
        }
    }
}

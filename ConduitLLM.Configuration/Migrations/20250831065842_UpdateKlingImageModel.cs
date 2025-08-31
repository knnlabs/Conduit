using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class UpdateKlingImageModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update kling-v2.1 as an image generation model
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsImageGeneration"" = true
                WHERE ""Name"" = 'kling-v2.1'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert kling-v2.1 image generation capability
            migrationBuilder.Sql(@"
                UPDATE ""Models"" SET 
                    ""SupportsImageGeneration"" = false
                WHERE ""Name"" = 'kling-v2.1'
            ");
        }
    }
}

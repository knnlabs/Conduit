using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class RemoveApiParametersReferencesClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if ApiParameters column exists in ModelSeries and drop it
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                              WHERE table_name = 'ModelSeries' 
                              AND column_name = 'ApiParameters') THEN
                        ALTER TABLE ""ModelSeries"" DROP COLUMN ""ApiParameters"";
                    END IF;
                END $$;
            ");

            // Check if ApiParameters column exists in ModelProviderMappings and drop it
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                              WHERE table_name = 'ModelProviderMappings' 
                              AND column_name = 'ApiParameters') THEN
                        ALTER TABLE ""ModelProviderMappings"" DROP COLUMN ""ApiParameters"";
                    END IF;
                END $$;
            ");

            // Check if ApiParameters column exists in Models and rename it to Parameters
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                              WHERE table_name = 'Models' 
                              AND column_name = 'ApiParameters') THEN
                        ALTER TABLE ""Models"" RENAME COLUMN ""ApiParameters"" TO ""Parameters"";
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse operations - rename Parameters back to ApiParameters if it exists
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                              WHERE table_name = 'Models' 
                              AND column_name = 'Parameters') THEN
                        ALTER TABLE ""Models"" RENAME COLUMN ""Parameters"" TO ""ApiParameters"";
                    END IF;
                END $$;
            ");

            // Re-add ApiParameters columns if they don't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                  WHERE table_name = 'ModelSeries' 
                                  AND column_name = 'ApiParameters') THEN
                        ALTER TABLE ""ModelSeries"" ADD COLUMN ""ApiParameters"" text;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                  WHERE table_name = 'ModelProviderMappings' 
                                  AND column_name = 'ApiParameters') THEN
                        ALTER TABLE ""ModelProviderMappings"" ADD COLUMN ""ApiParameters"" text;
                    END IF;
                END $$;
            ");
        }
    }
}
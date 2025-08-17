using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.SeedData;
using ConduitLLM.Configuration.Data;

namespace ConduitLLM.Configuration.Migrations
{
    /// <summary>
    /// Helper class for migrating existing ModelProviderMappings to the new Model architecture.
    /// This should be run as part of an EF migration or as a separate data migration script.
    /// </summary>
    public static class ModelArchitectureMigration
    {
        /// <summary>
        /// Seeds the initial model data into the database.
        /// Should be called from an EF migration's Up method.
        /// </summary>
        public static async Task SeedModelData(ConduitDbContext context)
        {
            var seedData = ModelSeedData.GetAllSeedData();

            // Add Authors if they don't exist
            foreach (var author in seedData.Authors)
            {
                if (!await context.Set<ModelAuthor>().AnyAsync(a => a.Id == author.Id))
                {
                    context.Set<ModelAuthor>().Add(author);
                }
            }

            // Add Capabilities if they don't exist
            foreach (var capability in seedData.Capabilities)
            {
                if (!await context.Set<ModelCapabilities>().AnyAsync(c => c.Id == capability.Id))
                {
                    context.Set<ModelCapabilities>().Add(capability);
                }
            }

            // Add Series if they don't exist
            foreach (var series in seedData.Series)
            {
                if (!await context.Set<ModelSeries>().AnyAsync(s => s.Id == series.Id))
                {
                    context.Set<ModelSeries>().Add(series);
                }
            }

            // Add Models if they don't exist
            foreach (var model in seedData.Models)
            {
                if (!await context.Set<Model>().AnyAsync(m => m.Id == model.Id))
                {
                    context.Set<Model>().Add(model);
                }
            }

            // Add Model Identifiers if they don't exist
            foreach (var identifier in seedData.Identifiers)
            {
                if (!await context.Set<ModelIdentifier>().AnyAsync(i => i.Id == identifier.Id))
                {
                    context.Set<ModelIdentifier>().Add(identifier);
                }
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Migrates existing ModelProviderMappings to use the new Model entities.
        /// Maps common model aliases to their corresponding Model IDs.
        /// </summary>
        public static async Task MigrateExistingMappings(ConduitDbContext context)
        {
            // Create a mapping of common aliases to model IDs
            var modelAliasMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                // GPT-4 variants
                { "gpt-4-turbo", 1 },
                { "gpt-4-turbo-preview", 1 },
                { "gpt-4-0125-preview", 1 },
                { "gpt-4-1106-preview", 1 },
                { "gpt-4-vision-preview", 1 },
                { "gpt-4", 2 },
                { "gpt-4-0613", 2 },
                { "gpt-4-0314", 2 },
                
                // GPT-3.5 variants
                { "gpt-3.5-turbo", 3 },
                { "gpt-3.5-turbo-0125", 3 },
                { "gpt-3.5-turbo-1106", 3 },
                { "gpt-3.5-turbo-16k", 3 },
                
                // Claude variants
                { "claude-3-opus", 4 },
                { "claude-3-opus-20240229", 4 },
                { "claude-3-sonnet", 5 },
                { "claude-3-sonnet-20240229", 5 },
                
                // Llama variants
                { "llama-3-70b", 6 },
                { "llama3-70b", 6 },
                { "meta-llama-3-70b", 6 },
                
                // Image/Audio models
                { "dall-e-3", 7 },
                { "whisper-1", 8 },
                { "tts-1-hd", 9 },
                { "text-embedding-3-large", 10 },
                { "text-embedding-ada-002", 10 }
            };

            // Get all mappings that need migration
            var mappings = await context.Set<ModelProviderMapping>()
                .Where(m => m.ModelId == 0) // Assuming 0 or null for unmigrated
                .ToListAsync();

            foreach (var mapping in mappings)
            {
                // Try to find a matching model ID
                if (modelAliasMap.TryGetValue(mapping.ModelAlias, out var modelId))
                {
                    mapping.ModelId = modelId;
                }
                else if (modelAliasMap.TryGetValue(mapping.ProviderModelId, out modelId))
                {
                    mapping.ModelId = modelId;
                }
                else
                {
                    // Log warning for unmapped model
                    Console.WriteLine($"Warning: No model mapping found for alias '{mapping.ModelAlias}' or provider model '{mapping.ProviderModelId}'");
                    
                    // For unmapped models, you might want to:
                    // 1. Create a generic "Unknown" model
                    // 2. Skip the mapping
                    // 3. Throw an exception
                    // For now, we'll skip
                    continue;
                }

                // Migrate capability overrides if the provider had different capabilities
                // This would require comparing the old capability flags with the Model's capabilities
                // and creating a JSON override for any differences
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Creates a SQL migration script for direct database migration.
        /// Use this if you prefer SQL over EF migrations.
        /// </summary>
        public static string GenerateSqlMigrationScript()
        {
            return @"
-- Insert Model Authors
INSERT INTO ""ModelAuthor"" (""Id"", ""Name"", ""Description"", ""WebsiteUrl"") VALUES
(1, 'OpenAI', 'OpenAI, creator of GPT models', 'https://openai.com'),
(2, 'Anthropic', 'Anthropic, creator of Claude models', 'https://anthropic.com'),
(3, 'Meta', 'Meta AI, creator of Llama models', 'https://ai.meta.com'),
(4, 'Google', 'Google AI, creator of Gemini models', 'https://ai.google'),
(5, 'Mistral AI', 'Mistral AI, creator of Mistral models', 'https://mistral.ai'),
(6, 'Stability AI', 'Stability AI, creator of Stable Diffusion', 'https://stability.ai'),
(7, 'Cohere', 'Cohere, creator of Command models', 'https://cohere.com'),
(8, 'Runway', 'Runway, creator of Gen video models', 'https://runwayml.com')
ON CONFLICT (""Id"") DO NOTHING;

-- Insert Model Capabilities
INSERT INTO ""ModelCapabilities"" (""Id"", ""MaxTokens"", ""MinTokens"", ""SupportsChat"", ""SupportsFunctionCalling"", ""SupportsStreaming"", ""SupportsVision"", ""TokenizerType"") VALUES
(1, 128000, 1, true, true, true, true, 0), -- GPT-4 Turbo
(2, 8192, 1, true, true, true, false, 0),   -- GPT-4
(3, 16384, 1, true, true, true, false, 0)   -- GPT-3.5 Turbo
ON CONFLICT (""Id"") DO NOTHING;

-- Insert Model Series
INSERT INTO ""ModelSeries"" (""Id"", ""AuthorId"", ""Name"", ""Description"", ""TokenizerType"", ""Parameters"") VALUES
(1, 1, 'GPT-4', 'OpenAI''s most capable language model series', 0, '{}'),
(2, 1, 'GPT-3.5', 'OpenAI''s fast and efficient language model series', 0, '{}'),
(3, 2, 'Claude 3', 'Anthropic''s advanced language model series', 5, '{}'),
(4, 3, 'Llama 3', 'Meta''s open-source language model series', 20, '{}'),
(5, 1, 'DALL-E', 'OpenAI''s image generation model series', 0, '{}')
ON CONFLICT (""Id"") DO NOTHING;

-- Insert Models
INSERT INTO ""Model"" (""Id"", ""Name"", ""ModelSeriesId"", ""ModelCapabilitiesId"", ""ModelType"", ""Description"") VALUES
(1, 'GPT-4 Turbo', 1, 1, 0, 'Latest GPT-4 Turbo with 128K context'),
(2, 'GPT-4', 1, 2, 0, 'Original GPT-4 with 8K context'),
(3, 'GPT-3.5 Turbo', 2, 3, 0, 'Fast and efficient model for most tasks')
ON CONFLICT (""Id"") DO NOTHING;

-- Update existing ModelProviderMappings to use Model IDs
UPDATE ""ModelProviderMapping"" 
SET ""ModelId"" = 
    CASE 
        WHEN ""ModelAlias"" ILIKE '%gpt-4-turbo%' OR ""ModelAlias"" ILIKE '%gpt-4-1106%' OR ""ModelAlias"" ILIKE '%gpt-4-0125%' THEN 1
        WHEN ""ModelAlias"" ILIKE 'gpt-4' OR ""ModelAlias"" ILIKE 'gpt-4-0613' THEN 2
        WHEN ""ModelAlias"" ILIKE '%gpt-3.5%' THEN 3
        WHEN ""ModelAlias"" ILIKE '%claude-3-opus%' THEN 4
        WHEN ""ModelAlias"" ILIKE '%claude-3-sonnet%' THEN 5
        WHEN ""ModelAlias"" ILIKE '%llama%70b%' THEN 6
        WHEN ""ModelAlias"" ILIKE '%dall-e-3%' THEN 7
        WHEN ""ModelAlias"" ILIKE '%whisper%' THEN 8
        WHEN ""ModelAlias"" ILIKE '%tts%' THEN 9
        WHEN ""ModelAlias"" ILIKE '%embedding%' THEN 10
        ELSE 1 -- Default to GPT-4 Turbo if unknown
    END
WHERE ""ModelId"" IS NULL OR ""ModelId"" = 0;
";
        }
    }
}
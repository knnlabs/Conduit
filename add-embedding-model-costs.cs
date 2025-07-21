using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core;

namespace ConduitLLM.Tools
{
    /// <summary>
    /// Tool to add embedding model costs for various providers
    /// </summary>
    public class EmbeddingModelCostSeeder
    {
        private readonly IModelCostService _modelCostService;
        private readonly ILogger<EmbeddingModelCostSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the EmbeddingModelCostSeeder class
        /// </summary>
        /// <param name="modelCostService">Model cost service</param>
        /// <param name="logger">Logger</param>
        public EmbeddingModelCostSeeder(IModelCostService modelCostService, ILogger<EmbeddingModelCostSeeder> logger)
        {
            _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Seeds the database with embedding model costs
        /// </summary>
        public async Task SeedEmbeddingModelCostsAsync()
        {
            try
            {
                _logger.LogInformation("Starting to seed embedding model costs");

                // OpenAI Embedding Models (already in main script, but ensuring completeness)
                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "openai/text-embedding-3-small",
                    embeddingCostPer1KTokens: 0.02m);

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "openai/text-embedding-3-large",
                    embeddingCostPer1KTokens: 0.13m);

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "openai/text-embedding-ada-002",
                    embeddingCostPer1KTokens: 0.10m);

                // Cohere Embedding Models
                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "cohere/embed-english-v3.0",
                    embeddingCostPer1KTokens: 0.10m);

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "cohere/embed-multilingual-v3.0",
                    embeddingCostPer1KTokens: 0.10m);

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "cohere/embed-english-light-v3.0",
                    embeddingCostPer1KTokens: 0.10m);

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "cohere/embed-multilingual-light-v3.0",
                    embeddingCostPer1KTokens: 0.10m);

                // AWS Bedrock - Cohere Embed Models
                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "bedrock/cohere.embed-english-v3",
                    embeddingCostPer1KTokens: 0.10m);

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "bedrock/cohere.embed-multilingual-v3",
                    embeddingCostPer1KTokens: 0.10m);

                // AWS Bedrock - Amazon Titan Embed Models
                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "bedrock/amazon.titan-embed-text-v1",
                    embeddingCostPer1KTokens: 0.10m);

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "bedrock/amazon.titan-embed-text-v2:0",
                    embeddingCostPer1KTokens: 0.02m);

                // HuggingFace Embedding Models (using feature-extraction task)
                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "huggingface/sentence-transformers/all-MiniLM-L6-v2",
                    embeddingCostPer1KTokens: 0.00m); // Free tier

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "huggingface/sentence-transformers/all-mpnet-base-v2",
                    embeddingCostPer1KTokens: 0.00m); // Free tier

                // Fireworks AI Embedding Models
                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "fireworks/nomic-ai/nomic-embed-text-v1.5",
                    embeddingCostPer1KTokens: 0.008m);

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "fireworks/WhereIsAI/UAE-Large-V1",
                    embeddingCostPer1KTokens: 0.008m);

                // Generic patterns for commonly used embedding models
                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "*text-embedding*",
                    embeddingCostPer1KTokens: 0.10m); // Default fallback

                await AddEmbeddingModelCostAsync(
                    modelIdPattern: "*embed*",
                    embeddingCostPer1KTokens: 0.10m); // Generic embed models

                _logger.LogInformation("Successfully seeded embedding model costs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding embedding model costs");
                throw;
            }
        }

        private async Task AddEmbeddingModelCostAsync(
            string modelIdPattern,
            decimal embeddingCostPer1KTokens)
        {
            try
            {
                // First check if a model cost with this pattern already exists
                var existingCosts = await _modelCostService.ListModelCostsAsync();
                var existingCost = existingCosts.Find(c => c.ModelIdPattern == modelIdPattern);

                if (existingCost != null)
                {
                    // Only update if embedding cost is not already set
                    if (!existingCost.EmbeddingTokenCost.HasValue)
                    {
                        _logger.LogInformation("Updating existing model cost for pattern '{ModelIdPattern}' with embedding cost", modelIdPattern);
                        existingCost.EmbeddingTokenCost = embeddingCostPer1KTokens / 1000; // Convert from per 1K tokens to per token
                        await _modelCostService.UpdateModelCostAsync(existingCost);
                    }
                    else
                    {
                        _logger.LogInformation("Model cost for pattern '{ModelIdPattern}' already has embedding cost set", modelIdPattern);
                    }
                }
                else
                {
                    _logger.LogInformation("Adding new embedding model cost for pattern '{ModelIdPattern}'", modelIdPattern);

                    // Create a new model cost for embedding-only models
                    var modelCost = new ModelCost
                    {
                        ModelIdPattern = modelIdPattern,
                        InputTokenCost = 0m, // Embedding models don't have input/output costs
                        OutputTokenCost = 0m,
                        EmbeddingTokenCost = embeddingCostPer1KTokens / 1000 // Convert from per 1K tokens to per token
                    };

                    await _modelCostService.AddModelCostAsync(modelCost);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating embedding model cost for pattern '{ModelIdPattern}'", modelIdPattern);
                throw;
            }
        }
    }

    /// <summary>
    /// Console application to run the embedding model cost seeder
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var serviceCollection = new ServiceCollection();

            // Add logging
            serviceCollection.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add DbContext
            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=conduit.db";
            serviceCollection.AddDbContext<ConfigurationDbContext>(options =>
                options.UseSqlite(connectionString));

            // Add repositories and services
            serviceCollection.AddScoped<IModelCostRepository, ModelCostRepository>();
            serviceCollection.AddScoped<IModelCostService, ModelCostService>();
            serviceCollection.AddMemoryCache();

            // Add the seeder
            serviceCollection.AddScoped<EmbeddingModelCostSeeder>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            try
            {
                using var scope = serviceProvider.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<EmbeddingModelCostSeeder>();
                await seeder.SeedEmbeddingModelCostsAsync();
                Console.WriteLine("Embedding model costs seeded successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
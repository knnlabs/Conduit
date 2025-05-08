using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core;

namespace ConduitLLM.Tools
{
    /// <summary>
    /// Tool to add frontier model costs for Anthropic and OpenAI models
    /// </summary>
    public class FrontierModelCostSeeder
    {
        private readonly IModelCostService _modelCostService;
        private readonly ILogger<FrontierModelCostSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the FrontierModelCostSeeder class
        /// </summary>
        /// <param name="modelCostService">Model cost service</param>
        /// <param name="logger">Logger</param>
        public FrontierModelCostSeeder(IModelCostService modelCostService, ILogger<FrontierModelCostSeeder> logger)
        {
            _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Seeds the database with frontier model costs
        /// </summary>
        public async Task SeedModelCostsAsync()
        {
            try
            {
                // Anthropic Models - Claude 3 Family
                await AddModelCostAsync(
                    modelIdPattern: "anthropic/claude-3-opus-20240229",
                    inputCostPer1KTokens: 15.00m,
                    outputCostPer1KTokens: 75.00m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "anthropic/claude-3-sonnet-20240229",
                    inputCostPer1KTokens: 3.00m,
                    outputCostPer1KTokens: 15.00m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "anthropic/claude-3-haiku-20240307",
                    inputCostPer1KTokens: 0.25m,
                    outputCostPer1KTokens: 1.25m);
                    
                // Add generic pattern for all Claude 3 models (for ease of use)
                await AddModelCostAsync(
                    modelIdPattern: "anthropic/claude-3*",
                    inputCostPer1KTokens: 3.00m,  // Default to Sonnet pricing
                    outputCostPer1KTokens: 15.00m);
                    
                // For Claude 2.1
                await AddModelCostAsync(
                    modelIdPattern: "anthropic/claude-2.1",
                    inputCostPer1KTokens: 8.00m,
                    outputCostPer1KTokens: 24.00m);
                    
                // For Claude 2.0
                await AddModelCostAsync(
                    modelIdPattern: "anthropic/claude-2.0",
                    inputCostPer1KTokens: 8.00m,
                    outputCostPer1KTokens: 24.00m);
                    
                // For Claude Instant 1.2
                await AddModelCostAsync(
                    modelIdPattern: "anthropic/claude-instant-1.2",
                    inputCostPer1KTokens: 0.80m,
                    outputCostPer1KTokens: 2.40m);
                
                // OpenAI Models - GPT-4 Family
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4o",
                    inputCostPer1KTokens: 5.00m,
                    outputCostPer1KTokens: 15.00m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4o-mini",
                    inputCostPer1KTokens: 0.50m,
                    outputCostPer1KTokens: 1.50m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4-turbo",
                    inputCostPer1KTokens: 10.00m,
                    outputCostPer1KTokens: 30.00m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4-1106-preview",
                    inputCostPer1KTokens: 10.00m,
                    outputCostPer1KTokens: 30.00m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4-0125-preview",
                    inputCostPer1KTokens: 10.00m,
                    outputCostPer1KTokens: 30.00m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4-vision-preview",
                    inputCostPer1KTokens: 10.00m,
                    outputCostPer1KTokens: 30.00m);
                
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4-32k",
                    inputCostPer1KTokens: 60.00m,
                    outputCostPer1KTokens: 120.00m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4",
                    inputCostPer1KTokens: 30.00m,
                    outputCostPer1KTokens: 60.00m);
                
                // Add generic pattern for all GPT-4 models (for ease of use)
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-4*",
                    inputCostPer1KTokens: 10.00m,  // Default to turbo pricing
                    outputCostPer1KTokens: 30.00m);
                
                // OpenAI Models - GPT-3.5 Family
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-3.5-turbo",
                    inputCostPer1KTokens: 0.50m,
                    outputCostPer1KTokens: 1.50m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-3.5-turbo-16k",
                    inputCostPer1KTokens: 1.00m,
                    outputCostPer1KTokens: 2.00m);
                    
                // Add generic pattern for all GPT-3.5 models (for ease of use)
                await AddModelCostAsync(
                    modelIdPattern: "openai/gpt-3.5*",
                    inputCostPer1KTokens: 0.50m,
                    outputCostPer1KTokens: 1.50m);
                
                // OpenAI - Embedding Models
                await AddModelCostAsync(
                    modelIdPattern: "openai/text-embedding-3-small",
                    inputCostPer1KTokens: 0.02m,
                    outputCostPer1KTokens: 0.00m,
                    embeddingCostPer1KTokens: 0.02m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/text-embedding-3-large",
                    inputCostPer1KTokens: 0.13m,
                    outputCostPer1KTokens: 0.00m,
                    embeddingCostPer1KTokens: 0.13m);
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/text-embedding-ada-002",
                    inputCostPer1KTokens: 0.10m,
                    outputCostPer1KTokens: 0.00m,
                    embeddingCostPer1KTokens: 0.10m);
                
                // OpenAI - Image Models (DALL-E)
                await AddModelCostAsync(
                    modelIdPattern: "openai/dall-e-3",
                    inputCostPer1KTokens: 0.00m,
                    outputCostPer1KTokens: 0.00m,
                    imageCostPerImage: 0.04m);  // Standard quality, 1024x1024
                    
                await AddModelCostAsync(
                    modelIdPattern: "openai/dall-e-2",
                    inputCostPer1KTokens: 0.00m,
                    outputCostPer1KTokens: 0.00m,
                    imageCostPerImage: 0.02m);  // Standard quality, 1024x1024
                
                _logger.LogInformation("Successfully seeded frontier model costs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding frontier model costs");
                throw;
            }
        }

        private async Task AddModelCostAsync(
            string modelIdPattern, 
            decimal inputCostPer1KTokens, 
            decimal outputCostPer1KTokens, 
            decimal? embeddingCostPer1KTokens = null, 
            decimal? imageCostPerImage = null)
        {
            try
            {
                // First check if a model cost with this pattern already exists
                var existingCosts = await _modelCostService.ListModelCostsAsync();
                var existingCost = existingCosts.Find(c => c.ModelIdPattern == modelIdPattern);
                
                if (existingCost != null)
                {
                    _logger.LogInformation("Model cost for pattern '{ModelIdPattern}' already exists, updating", modelIdPattern);
                    
                    // Update the existing cost
                    existingCost.InputTokenCost = inputCostPer1KTokens / 1000;
                    existingCost.OutputTokenCost = outputCostPer1KTokens / 1000;
                    existingCost.EmbeddingTokenCost = embeddingCostPer1KTokens.HasValue ? embeddingCostPer1KTokens.Value / 1000 : null;
                    existingCost.ImageCostPerImage = imageCostPerImage;
                    
                    await _modelCostService.UpdateModelCostAsync(existingCost);
                }
                else
                {
                    _logger.LogInformation("Adding model cost for pattern '{ModelIdPattern}'", modelIdPattern);
                    
                    // Create a new model cost
                    var modelCost = new ModelCost
                    {
                        ModelIdPattern = modelIdPattern,
                        InputTokenCost = inputCostPer1KTokens / 1000,      // Convert from per 1K tokens to per token
                        OutputTokenCost = outputCostPer1KTokens / 1000,     // Convert from per 1K tokens to per token
                        EmbeddingTokenCost = embeddingCostPer1KTokens.HasValue ? embeddingCostPer1KTokens.Value / 1000 : null,
                        ImageCostPerImage = imageCostPerImage
                    };
                    
                    await _modelCostService.AddModelCostAsync(modelCost);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating model cost for pattern '{ModelIdPattern}'", modelIdPattern);
                throw;
            }
        }
    }
    
    /// <summary>
    /// Program to seed frontier model costs
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point for the program
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static async Task Main(string[] args)
        {
            // Configure the service collection
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder.AddConsole())
                .AddDbContextFactory<ConfigurationDbContext>(options =>
                {
                    var (dbProvider, dbConnectionString) = DbConnectionHelper.GetProviderAndConnectionString();
                    
                    if (dbProvider == "sqlite")
                    {
                        options.UseSqlite(dbConnectionString);
                    }
                    else if (dbProvider == "postgres")
                    {
                        options.UseNpgsql(dbConnectionString);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unsupported database provider: {dbProvider}. Supported values are 'sqlite' and 'postgres'.");
                    }
                })
                .AddMemoryCache()
                .AddScoped<IModelCostRepository, ModelCostRepository>()
                .AddScoped<IModelCostService, ModelCostService>()
                .AddScoped<FrontierModelCostSeeder>()
                .BuildServiceProvider();

            // Get the seed service and run it
            using (var scope = serviceProvider.CreateScope())
            {
                var seeder = scope.ServiceProvider.GetRequiredService<FrontierModelCostSeeder>();
                await seeder.SeedModelCostsAsync();
            }
        }
    }
}
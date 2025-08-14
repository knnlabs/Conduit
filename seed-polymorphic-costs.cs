using System;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Tools
{
    /// <summary>
    /// Tool to seed polymorphic model costs for MiniMax, Replicate, and Fireworks
    /// </summary>
    public class PolymorphicModelCostSeeder
    {
        private readonly ConfigurationDbContext _context;

        public PolymorphicModelCostSeeder(ConfigurationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task SeedPolymorphicCostsAsync()
        {
            // MiniMax Video Models (Per-Video Flat Rate)
            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "MiniMax Hailuo-02 Video",
                PricingModel = PricingModel.PerVideo,
                PricingConfiguration = JsonSerializer.Serialize(new PerVideoPricingConfig
                {
                    Rates = new Dictionary<string, decimal>
                    {
                        ["512p_6"] = 0.10m,
                        ["512p_10"] = 0.15m,
                        ["768p_6"] = 0.28m,
                        ["768p_10"] = 0.56m,
                        ["1080p_6"] = 0.49m
                    }
                }),
                ModelType = "video",
                IsActive = true,
                Priority = 100
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "MiniMax S2V-01 Video",
                PricingModel = PricingModel.PerVideo,
                PricingConfiguration = JsonSerializer.Serialize(new PerVideoPricingConfig
                {
                    Rates = new Dictionary<string, decimal>
                    {
                        ["standard_5"] = 0.65m,
                        ["standard_10"] = 0.65m
                    }
                }),
                ModelType = "video",
                IsActive = true,
                Priority = 90
            });

            // MiniMax Text Models
            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "MiniMax-M1 Tiered",
                PricingModel = PricingModel.TieredTokens,
                PricingConfiguration = JsonSerializer.Serialize(new TieredTokensPricingConfig
                {
                    Tiers = new List<TokenPricingTier>
                    {
                        new TokenPricingTier { MaxContext = 200000, InputCost = 400, OutputCost = 2200 },
                        new TokenPricingTier { MaxContext = null, InputCost = 1300, OutputCost = 2200 }
                    }
                }),
                ModelType = "chat",
                IsActive = true,
                Priority = 100
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "MiniMax-Text-01",
                PricingModel = PricingModel.Standard,
                InputCostPerMillionTokens = 200,
                OutputCostPerMillionTokens = 1100,
                ModelType = "chat",
                IsActive = true,
                Priority = 90
            });

            // MiniMax Image Model
            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "MiniMax Image-01",
                PricingModel = PricingModel.PerImage,
                PricingConfiguration = JsonSerializer.Serialize(new PerImagePricingConfig
                {
                    BaseRate = 0.0035m,
                    QualityMultipliers = new Dictionary<string, decimal>
                    {
                        ["standard"] = 1.0m,
                        ["hd"] = 1.5m
                    },
                    ResolutionMultipliers = new Dictionary<string, decimal>
                    {
                        ["512x512"] = 0.5m,
                        ["1024x1024"] = 1.0m,
                        ["1792x1024"] = 1.5m,
                        ["1024x1792"] = 1.5m
                    }
                }),
                ImageCostPerImage = 0.0035m,
                ModelType = "image",
                IsActive = true,
                Priority = 100
            });

            // MiniMax Audio (TTS)
            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "MiniMax Speech-2.5-turbo",
                PricingModel = PricingModel.Standard,
                AudioCostPerKCharacters = 0.06m,
                ModelType = "audio",
                IsActive = true,
                Priority = 90
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "MiniMax Speech-2.5-hd",
                PricingModel = PricingModel.Standard,
                AudioCostPerKCharacters = 0.10m,
                ModelType = "audio",
                IsActive = true,
                Priority = 100
            });

            // Replicate Video Models (Per-Second with Resolution Multipliers)
            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Replicate MiniMax Video",
                PricingModel = PricingModel.PerSecondVideo,
                PricingConfiguration = JsonSerializer.Serialize(new PerSecondVideoPricingConfig
                {
                    BaseRate = 0.09m,
                    ResolutionMultipliers = new Dictionary<string, decimal>
                    {
                        ["480p"] = 0.5m,
                        ["720p"] = 1.0m,
                        ["1080p"] = 1.5m,
                        ["4k"] = 2.5m
                    }
                }),
                VideoCostPerSecond = 0.09m,
                ModelType = "video",
                IsActive = true,
                Priority = 100
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Replicate Google Veo-2",
                PricingModel = PricingModel.PerSecondVideo,
                PricingConfiguration = JsonSerializer.Serialize(new PerSecondVideoPricingConfig
                {
                    BaseRate = 0.50m,
                    ResolutionMultipliers = new Dictionary<string, decimal>
                    {
                        ["480p"] = 0.6m,
                        ["720p"] = 1.0m,
                        ["1080p"] = 1.5m
                    }
                }),
                VideoCostPerSecond = 0.50m,
                ModelType = "video",
                IsActive = true,
                Priority = 110
            });

            // Replicate Image Models
            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Replicate Flux 1.1 Pro",
                PricingModel = PricingModel.PerImage,
                PricingConfiguration = JsonSerializer.Serialize(new PerImagePricingConfig
                {
                    BaseRate = 0.04m,
                    QualityMultipliers = new Dictionary<string, decimal>
                    {
                        ["standard"] = 1.0m,
                        ["hd"] = 1.5m
                    },
                    ResolutionMultipliers = new Dictionary<string, decimal>
                    {
                        ["512x512"] = 0.5m,
                        ["1024x1024"] = 1.0m,
                        ["1792x1024"] = 1.25m,
                        ["1024x1792"] = 1.25m,
                        ["2048x2048"] = 2.0m
                    }
                }),
                ImageCostPerImage = 0.04m,
                ModelType = "image",
                IsActive = true,
                Priority = 100
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Replicate Flux Dev",
                PricingModel = PricingModel.PerImage,
                PricingConfiguration = JsonSerializer.Serialize(new PerImagePricingConfig
                {
                    BaseRate = 0.025m,
                    ResolutionMultipliers = new Dictionary<string, decimal>
                    {
                        ["512x512"] = 0.5m,
                        ["1024x1024"] = 1.0m,
                        ["1792x1024"] = 1.25m,
                        ["1024x1792"] = 1.25m
                    }
                }),
                ImageCostPerImage = 0.025m,
                ModelType = "image",
                IsActive = true,
                Priority = 90
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Replicate Flux Schnell",
                PricingModel = PricingModel.PerImage,
                PricingConfiguration = JsonSerializer.Serialize(new PerImagePricingConfig
                {
                    BaseRate = 0.003m
                }),
                ImageCostPerImage = 0.003m,
                ModelType = "image",
                IsActive = true,
                Priority = 80
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Replicate SDXL",
                PricingModel = PricingModel.PerImage,
                PricingConfiguration = JsonSerializer.Serialize(new PerImagePricingConfig
                {
                    BaseRate = 0.003m,
                    QualityMultipliers = new Dictionary<string, decimal>
                    {
                        ["standard"] = 1.0m,
                        ["detailed"] = 1.5m
                    },
                    ResolutionMultipliers = new Dictionary<string, decimal>
                    {
                        ["512x512"] = 0.8m,
                        ["768x768"] = 0.9m,
                        ["1024x1024"] = 1.0m,
                        ["1024x1792"] = 1.3m
                    }
                }),
                ImageCostPerImage = 0.003m,
                ModelType = "image",
                IsActive = true,
                Priority = 85
            });

            // Fireworks AI Image Models (Inference Step Based)
            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Fireworks FLUX.1[schnell]",
                PricingModel = PricingModel.InferenceSteps,
                PricingConfiguration = JsonSerializer.Serialize(new InferenceStepsPricingConfig
                {
                    CostPerStep = 0.00035m,
                    DefaultSteps = 4,
                    ModelSteps = new Dictionary<string, int>
                    {
                        ["flux-schnell"] = 4,
                        ["flux-schnell-fast"] = 2
                    }
                }),
                CostPerInferenceStep = 0.00035m,
                DefaultInferenceSteps = 4,
                ModelType = "image",
                IsActive = true,
                Priority = 100
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Fireworks FLUX.1[dev]",
                PricingModel = PricingModel.InferenceSteps,
                PricingConfiguration = JsonSerializer.Serialize(new InferenceStepsPricingConfig
                {
                    CostPerStep = 0.00025m,
                    DefaultSteps = 20,
                    ModelSteps = new Dictionary<string, int>
                    {
                        ["flux-dev"] = 20,
                        ["flux-dev-fast"] = 10,
                        ["flux-dev-quality"] = 30
                    }
                }),
                CostPerInferenceStep = 0.00025m,
                DefaultInferenceSteps = 20,
                ModelType = "image",
                IsActive = true,
                Priority = 95
            });

            await AddOrUpdateModelCostAsync(new ModelCost
            {
                CostName = "Fireworks SDXL",
                PricingModel = PricingModel.InferenceSteps,
                PricingConfiguration = JsonSerializer.Serialize(new InferenceStepsPricingConfig
                {
                    CostPerStep = 0.00013m,
                    DefaultSteps = 30,
                    ModelSteps = new Dictionary<string, int>
                    {
                        ["sdxl"] = 30,
                        ["sdxl-fast"] = 15,
                        ["sdxl-quality"] = 50
                    }
                }),
                CostPerInferenceStep = 0.00013m,
                DefaultInferenceSteps = 30,
                ModelType = "image",
                IsActive = true,
                Priority = 90
            });

            await _context.SaveChangesAsync();
            Console.WriteLine("Successfully seeded polymorphic model costs for MiniMax, Replicate, and Fireworks");
        }

        private async Task AddOrUpdateModelCostAsync(ModelCost modelCost)
        {
            var existing = await _context.ModelCosts
                .FirstOrDefaultAsync(c => c.CostName == modelCost.CostName);

            if (existing != null)
            {
                // Update existing
                existing.PricingModel = modelCost.PricingModel;
                existing.PricingConfiguration = modelCost.PricingConfiguration;
                existing.InputCostPerMillionTokens = modelCost.InputCostPerMillionTokens;
                existing.OutputCostPerMillionTokens = modelCost.OutputCostPerMillionTokens;
                existing.ImageCostPerImage = modelCost.ImageCostPerImage;
                existing.VideoCostPerSecond = modelCost.VideoCostPerSecond;
                existing.AudioCostPerKCharacters = modelCost.AudioCostPerKCharacters;
                existing.CostPerInferenceStep = modelCost.CostPerInferenceStep;
                existing.DefaultInferenceSteps = modelCost.DefaultInferenceSteps;
                existing.ModelType = modelCost.ModelType;
                existing.IsActive = modelCost.IsActive;
                existing.Priority = modelCost.Priority;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Add new
                modelCost.CreatedAt = DateTime.UtcNow;
                modelCost.UpdatedAt = DateTime.UtcNow;
                modelCost.EffectiveDate = DateTime.UtcNow;
                _context.ModelCosts.Add(modelCost);
            }
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var serviceProvider = new ServiceCollection()
                .AddDbContext<ConfigurationDbContext>(options =>
                {
                    var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
                        ?? "Host=localhost;Database=conduitdb;Username=conduit;Password=conduitpass";
                    options.UseNpgsql(connectionString);
                })
                .AddScoped<PolymorphicModelCostSeeder>()
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var seeder = scope.ServiceProvider.GetRequiredService<PolymorphicModelCostSeeder>();
                await seeder.SeedPolymorphicCostsAsync();
            }
        }
    }
}
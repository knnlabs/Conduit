using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Tests.TestInfrastructure;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Tests specifically for the new ModelProviderTypeAssociation cost association functionality
    /// </summary>
    public class ModelProviderTypeAssociationCostTests : IDisposable
    {
        private readonly Mock<IModelCostRepository> _mockModelCostRepository;
        private readonly Mock<IModelProviderMappingRepository> _mockModelProviderMappingRepository;
        private readonly Mock<ILogger<ModelCostService>> _mockLogger;
        private readonly ModelCostService _service;
        private readonly DbContextOptions<ConduitDbContext> _dbOptions;
        private readonly SqliteTestDatabase _database;

        public ModelProviderTypeAssociationCostTests()
        {
            _mockModelCostRepository = new Mock<IModelCostRepository>();
            _mockModelProviderMappingRepository = new Mock<IModelProviderMappingRepository>();
            _mockLogger = new Mock<ILogger<ModelCostService>>();

            _service = new ModelCostService(
                _mockModelCostRepository.Object,
                _mockModelProviderMappingRepository.Object,
                _mockLogger.Object
            );

            _database = new SqliteTestDatabase();
            _dbOptions = _database.Options;
        }

        [Fact]
        public async Task GetCostForModelAsync_WithDirectAssociation_ShouldReturnCorrectCost()
        {
            // Arrange
            var modelIdentifier = "gpt-4-turbo";
            var expectedCost = new ModelCost
            {
                Id = 1,
                CostName = "GPT-4 Turbo Pricing",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-10),
                ModelProviderTypeAssociations = new List<ModelProviderTypeAssociation>
                {
                    new ModelProviderTypeAssociation
                    {
                        Id = 1,
                        Identifier = modelIdentifier,
                        ModelCostId = 1,
                        IsEnabled = true,
                        ModelId = 1
                    }
                }
            };

            var costs = new List<ModelCost> { expectedCost };
            _mockModelCostRepository.Setup(x => x.GetPaginatedAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((costs, costs.Count));

            // Act
            var result = await _service.GetCostForModelAsync(modelIdentifier);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
            result.CostName.Should().Be("GPT-4 Turbo Pricing");
            result.InputCostPerMillionTokens.Should().Be(10.00m);
            result.OutputCostPerMillionTokens.Should().Be(30.00m);
        }

        [Fact]
        public async Task GetCostForModelAsync_WithMultipleCosts_ShouldReturnHighestPriority()
        {
            // Arrange
            var modelIdentifier = "claude-3-opus";
            var costs = new List<ModelCost>
            {
                new ModelCost
                {
                    Id = 1,
                    CostName = "Claude Standard",
                    InputCostPerMillionTokens = 15.00m,
                    OutputCostPerMillionTokens = 75.00m,
                    Priority = 0,
                    IsActive = true,
                    EffectiveDate = DateTime.UtcNow.AddDays(-30),
                    ModelProviderTypeAssociations = new List<ModelProviderTypeAssociation>
                    {
                        new ModelProviderTypeAssociation { Identifier = modelIdentifier, IsEnabled = true, ModelId = 1 }
                    }
                },
                new ModelCost
                {
                    Id = 2,
                    CostName = "Claude Premium",
                    InputCostPerMillionTokens = 12.00m,
                    OutputCostPerMillionTokens = 60.00m,
                    Priority = 10, // Higher priority
                    IsActive = true,
                    EffectiveDate = DateTime.UtcNow.AddDays(-10),
                    ModelProviderTypeAssociations = new List<ModelProviderTypeAssociation>
                    {
                        new ModelProviderTypeAssociation { Identifier = modelIdentifier, IsEnabled = true, ModelId = 1 }
                    }
                }
            };

            _mockModelCostRepository.Setup(x => x.GetPaginatedAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((costs, costs.Count));

            // Act
            var result = await _service.GetCostForModelAsync(modelIdentifier);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(2); // Should return the one with higher priority
            result.CostName.Should().Be("Claude Premium");
        }

        [Fact]
        public async Task GetCostForModelAsync_WithDisabledAssociation_ShouldNotReturnCost()
        {
            // Arrange
            var modelIdentifier = "llama-3-70b";
            var cost = new ModelCost
            {
                Id = 1,
                CostName = "Llama 3 Pricing",
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-10),
                ModelProviderTypeAssociations = new List<ModelProviderTypeAssociation>
                {
                    new ModelProviderTypeAssociation
                    {
                        Identifier = modelIdentifier,
                        ModelCostId = 1,
                        IsEnabled = false, // Disabled association
                        ModelId = 1
                    }
                }
            };

            var costs = new List<ModelCost> { cost };
            _mockModelCostRepository.Setup(x => x.GetPaginatedAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((costs, costs.Count));

            // Act
            var result = await _service.GetCostForModelAsync(modelIdentifier);

            // Assert
            result.Should().BeNull(); // Should not return cost for disabled association
        }

        [Fact]
        public async Task GetCostForModelAsync_WithExpiredCost_ShouldNotReturnCost()
        {
            // Arrange
            var modelIdentifier = "mistral-7b";
            var cost = new ModelCost
            {
                Id = 1,
                CostName = "Mistral Pricing",
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-30),
                ExpiryDate = DateTime.UtcNow.AddDays(-1), // Expired yesterday
                ModelProviderTypeAssociations = new List<ModelProviderTypeAssociation>
                {
                    new ModelProviderTypeAssociation
                    {
                        Identifier = modelIdentifier,
                        ModelCostId = 1,
                        IsEnabled = true,
                        ModelId = 1
                    }
                }
            };

            var costs = new List<ModelCost> { cost };
            _mockModelCostRepository.Setup(x => x.GetPaginatedAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((costs, costs.Count));

            // Act
            var result = await _service.GetCostForModelAsync(modelIdentifier);

            // Assert
            result.Should().BeNull(); // Should not return expired cost
        }

        [Fact]
        public async Task ModelProviderTypeAssociation_CanHaveNullCostId()
        {
            // This test verifies that associations can exist without a cost
            // which is important for the new architecture
            using (var context = new ConduitDbContext(_dbOptions))
            {
                // Arrange
                AddModels(context, 1);
                var association = new ModelProviderTypeAssociation
                {
                    Id = 1,
                    Identifier = "some-model",
                    ModelId = 1,
                    ModelCostId = null, // No cost associated
                    IsEnabled = true
                };

                // Act
                context.ModelProviderTypeAssociations.Add(association);
                await context.SaveChangesAsync();

                // Assert
                var saved = await context.ModelProviderTypeAssociations.FindAsync(1);
                saved.Should().NotBeNull();
                saved!.ModelCostId.Should().BeNull();
            }
        }

        [Fact]
        public async Task MultiplAssociations_CanShareSameCost()
        {
            // This test verifies that multiple associations can reference the same cost
            using (var context = new ConduitDbContext(_dbOptions))
            {
                // Arrange
                AddModels(context, 1, 2);
                var cost = new ModelCost
                {
                    Id = 1,
                    CostName = "Shared Pricing",
                    InputCostPerMillionTokens = 5.00m,
                    OutputCostPerMillionTokens = 15.00m
                };

                var associations = new[]
                {
                    new ModelProviderTypeAssociation
                    {
                        Id = 1,
                        Identifier = "model-variant-1",
                        ModelId = 1,
                        ModelCostId = 1,
                        IsEnabled = true
                    },
                    new ModelProviderTypeAssociation
                    {
                        Id = 2,
                        Identifier = "model-variant-2",
                        ModelId = 1,
                        ModelCostId = 1, // Same cost ID
                        IsEnabled = true
                    },
                    new ModelProviderTypeAssociation
                    {
                        Id = 3,
                        Identifier = "model-variant-3",
                        ModelId = 2,
                        ModelCostId = 1, // Same cost ID
                        IsEnabled = true
                    }
                };

                // Act
                context.ModelCosts.Add(cost);
                context.ModelProviderTypeAssociations.AddRange(associations);
                await context.SaveChangesAsync();

                // Assert
                var associationsWithCost = await context.ModelProviderTypeAssociations
                    .Where(a => a.ModelCostId == 1)
                    .ToListAsync();
                
                associationsWithCost.Should().HaveCount(3);
                associationsWithCost.Should().AllSatisfy(a => a.ModelCostId.Should().Be(1));
            }
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private static void AddModels(ConduitDbContext context, params int[] modelIds)
        {
            foreach (var modelId in modelIds)
            {
                context.Models.Add(new Model
                {
                    Id = modelId,
                    Name = $"association-model-{modelId}",
                    Series = new ModelSeries
                    {
                        Id = modelId,
                        Name = $"association-series-{modelId}",
                        Parameters = "{}",
                        Author = new ModelAuthor
                        {
                            Id = modelId,
                            Name = $"association-author-{modelId}"
                        }
                    }
                });
            }
        }
    }
}

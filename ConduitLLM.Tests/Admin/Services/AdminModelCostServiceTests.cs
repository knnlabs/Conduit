using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    public class AdminModelCostServiceTests : IDisposable
    {
        private readonly Mock<IModelCostRepository> _mockModelCostRepository;
        private readonly Mock<IRequestLogRepository> _mockRequestLogRepository;
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockDbContextFactory;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<AdminModelCostService>> _mockLogger;
        private readonly AdminModelCostService _service;
        private readonly DbContextOptions<ConduitDbContext> _dbContextOptions;

        public AdminModelCostServiceTests()
        {
            _mockModelCostRepository = new Mock<IModelCostRepository>();
            _mockRequestLogRepository = new Mock<IRequestLogRepository>();
            _mockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<AdminModelCostService>>();

            // Setup in-memory database options for testing
            _dbContextOptions = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            // Setup factory to create new contexts each time
            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConduitDbContext(_dbContextOptions));

            _service = new AdminModelCostService(
                _mockModelCostRepository.Object,
                _mockRequestLogRepository.Object,
                _mockDbContextFactory.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object);
        }

        public void Dispose()
        {
            // Cleanup any remaining contexts if needed
        }

        private ConduitDbContext CreateDbContext()
        {
            return new ConduitDbContext(_dbContextOptions);
        }

        #region CreateModelCostAsync Tests

        [Fact]
        public async Task CreateModelCostAsync_WithValidData_ShouldCreateModelCost()
        {
            // Arrange
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Model Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = new List<int>()
            };

            var createdEntity = new ModelCost
            {
                Id = 1,
                CostName = createDto.CostName,
                InputCostPerMillionTokens = createDto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = createDto.OutputCostPerMillionTokens,
                ModelCostMappings = new List<ModelCostMapping>()
            };

            _mockModelCostRepository.Setup(x => x.GetByCostNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);
            _mockModelCostRepository.Setup(x => x.CreateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdEntity);

            // Act
            var result = await _service.CreateModelCostAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.CostName.Should().Be("Test Model Cost");
            result.InputCostPerMillionTokens.Should().Be(10.00m);
            result.OutputCostPerMillionTokens.Should().Be(20.00m);
        }

        [Fact]
        public async Task CreateModelCostAsync_WithModelProviderMappingIds_ShouldCreateMappings()
        {
            // Arrange
            var mappingIds = new List<int> { 1, 2, 3 };
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Model Cost with Mappings",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = mappingIds
            };

            var createdEntity = new ModelCost
            {
                Id = 1,
                CostName = createDto.CostName,
                InputCostPerMillionTokens = createDto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = createDto.OutputCostPerMillionTokens,
                ModelCostMappings = new List<ModelCostMapping>
                {
                    new ModelCostMapping 
                    { 
                        ModelCostId = 1, 
                        ModelProviderMappingId = 1, 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { Id = 1, ModelAlias = "model1" }
                    },
                    new ModelCostMapping 
                    { 
                        ModelCostId = 1, 
                        ModelProviderMappingId = 2, 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { Id = 2, ModelAlias = "model2" }
                    },
                    new ModelCostMapping 
                    { 
                        ModelCostId = 1, 
                        ModelProviderMappingId = 3, 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { Id = 3, ModelAlias = "model3" }
                    }
                }
            };

            _mockModelCostRepository.Setup(x => x.GetByCostNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);
            _mockModelCostRepository.Setup(x => x.CreateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdEntity);

            // Act
            var result = await _service.CreateModelCostAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.AssociatedModelAliases.Should().HaveCount(3);
            result.AssociatedModelAliases.Should().Contain(new[] { "model1", "model2", "model3" });
            
            // Verify mappings were added to DbContext
            using (var dbContext = CreateDbContext())
            {
                var mappings = dbContext.ModelCostMappings.ToList();
                mappings.Should().HaveCount(3);
                mappings.Should().AllSatisfy(m => 
                {
                    m.ModelCostId.Should().Be(1);
                    m.IsActive.Should().BeTrue();
                });
            }
        }

        [Fact]
        public async Task CreateModelCostAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var createDto = new CreateModelCostDto
            {
                CostName = "Existing Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            var existingCost = new ModelCost { Id = 1, CostName = "Existing Cost" };

            _mockModelCostRepository.Setup(x => x.GetByCostNameAsync("Existing Cost", It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.CreateModelCostAsync(createDto));
        }

        [Fact]
        public async Task CreateModelCostAsync_WithNullDto_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _service.CreateModelCostAsync(null!));
        }

        #endregion

        #region UpdateModelCostAsync Tests

        [Fact]
        public async Task UpdateModelCostAsync_WithValidData_ShouldUpdateModelCost()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
                CostName = "Updated Cost Name",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderMappingIds = new List<int>()
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost Name",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);
            _mockModelCostRepository.Setup(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert
            result.Should().BeTrue();
            _mockModelCostRepository.Verify(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithModelProviderMappingIds_ShouldUpdateMappings()
        {
            // Arrange
            var newMappingIds = new List<int> { 4, 5 };
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
                CostName = "Updated Cost",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderMappingIds = newMappingIds
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            // Add existing mappings to test context
            using (var setupContext = CreateDbContext())
            {
                setupContext.ModelCostMappings.AddRange(new[]
                {
                    new ModelCostMapping { Id = 1, ModelCostId = 1, ModelProviderMappingId = 1, IsActive = true },
                    new ModelCostMapping { Id = 2, ModelCostId = 1, ModelProviderMappingId = 2, IsActive = true },
                    new ModelCostMapping { Id = 3, ModelCostId = 1, ModelProviderMappingId = 3, IsActive = true }
                });
                await setupContext.SaveChangesAsync();
            }

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);
            _mockModelCostRepository.Setup(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert
            result.Should().BeTrue();
            
            // Verify old mappings were removed and new ones added
            using (var verifyContext = CreateDbContext())
            {
                var mappings = verifyContext.ModelCostMappings.Where(m => m.ModelCostId == 1).ToList();
                mappings.Should().HaveCount(2);
                mappings.Select(m => m.ModelProviderMappingId).Should().BeEquivalentTo(new[] { 4, 5 });
                mappings.Should().AllSatisfy(m => m.IsActive.Should().BeTrue());
            }
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 999,
                CostName = "Non-existent",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert
            result.Should().BeFalse();
            _mockModelCostRepository.Verify(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
                CostName = "Existing Other Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            var otherCost = new ModelCost
            {
                Id = 2,
                CostName = "Existing Other Cost"
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);
            _mockModelCostRepository.Setup(x => x.GetByCostNameAsync("Existing Other Cost", It.IsAny<CancellationToken>()))
                .ReturnsAsync(otherCost);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.UpdateModelCostAsync(updateDto));
        }

        #endregion

        #region DeleteModelCostAsync Tests

        [Fact]
        public async Task DeleteModelCostAsync_WithExistingId_ShouldReturnTrue()
        {
            // Arrange
            _mockModelCostRepository.Setup(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteModelCostAsync(1);

            // Assert
            result.Should().BeTrue();
            _mockModelCostRepository.Verify(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteModelCostAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            _mockModelCostRepository.Setup(x => x.DeleteAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.DeleteModelCostAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetModelCostByIdAsync Tests

        [Fact]
        public async Task GetModelCostByIdAsync_WithValidId_ShouldReturnModelCost()
        {
            // Arrange
            var modelCost = new ModelCost
            {
                Id = 1,
                CostName = "Test Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelCostMappings = new List<ModelCostMapping>
                {
                    new ModelCostMapping 
                    { 
                        ModelProviderMappingId = 1, 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { ModelAlias = "gpt-4" }
                    }
                }
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.GetModelCostByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.CostName.Should().Be("Test Cost");
            result.AssociatedModelAliases.Should().Contain("gpt-4");
        }

        [Fact]
        public async Task GetModelCostByIdAsync_WithNonExistentId_ShouldReturnNull()
        {
            // Arrange
            _mockModelCostRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.GetModelCostByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region Edge Cases and Bug Prevention Tests

        [Fact]
        public async Task CreateModelCostAsync_WithEmptyMappingIds_ShouldNotCreateMappings()
        {
            // This test ensures we handle empty mapping lists correctly
            var createDto = new CreateModelCostDto
            {
                CostName = "Cost without mappings",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = new List<int>() // Empty list
            };

            var createdEntity = new ModelCost
            {
                Id = 1,
                CostName = createDto.CostName,
                ModelCostMappings = new List<ModelCostMapping>()
            };

            _mockModelCostRepository.Setup(x => x.GetByCostNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);
            _mockModelCostRepository.Setup(x => x.CreateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdEntity);

            // Act
            var result = await _service.CreateModelCostAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.AssociatedModelAliases.Should().BeEmpty();
            using (var verifyContext = CreateDbContext())
            {
                verifyContext.ModelCostMappings.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task UpdateModelCostAsync_RemoveAllMappings_ShouldClearMappings()
        {
            // This test ensures we can remove all mappings by passing an empty list
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
                CostName = "Updated Cost",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderMappingIds = new List<int>() // Empty list to clear all mappings
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost"
            };

            // Add existing mappings
            using (var setupContext = CreateDbContext())
            {
                setupContext.ModelCostMappings.AddRange(new[]
                {
                    new ModelCostMapping { Id = 1, ModelCostId = 1, ModelProviderMappingId = 1 },
                    new ModelCostMapping { Id = 2, ModelCostId = 1, ModelProviderMappingId = 2 }
                });
                await setupContext.SaveChangesAsync();
            }

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);
            _mockModelCostRepository.Setup(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert
            result.Should().BeTrue();
            using (var verifyContext = CreateDbContext())
            {
                verifyContext.ModelCostMappings.Where(m => m.ModelCostId == 1).Should().BeEmpty();
            }
        }

        [Fact]
        public async Task ToDto_WithInactiveMappings_ShouldOnlyReturnActiveModelAliases()
        {
            // This test verifies the bug fix where only active mappings should be included
            var modelCost = new ModelCost
            {
                Id = 1,
                CostName = "Test Cost",
                ModelCostMappings = new List<ModelCostMapping>
                {
                    new ModelCostMapping 
                    { 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { ModelAlias = "active-model" }
                    },
                    new ModelCostMapping 
                    { 
                        IsActive = false, // Inactive mapping
                        ModelProviderMapping = new ModelProviderMapping { ModelAlias = "inactive-model" }
                    }
                }
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.GetModelCostByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.AssociatedModelAliases.Should().HaveCount(1);
            result.AssociatedModelAliases.Should().Contain("active-model");
            result.AssociatedModelAliases.Should().NotContain("inactive-model");
        }

        #endregion
    }
}
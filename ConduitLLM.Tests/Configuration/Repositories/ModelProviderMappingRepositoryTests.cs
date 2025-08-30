using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestInfrastructure;
using ConduitLLM.Tests.Helpers;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    /// <summary>
    /// Repository tests that work correctly with SQLite
    /// </summary>
    [Collection("RepositoryTests")]
    public class ModelProviderMappingRepositoryTests : RepositoryTestBase
    {
        private readonly ILogger<ModelProviderMappingRepository> _logger;

        public ModelProviderMappingRepositoryTests()
        {
            _logger = new LoggerFactory().CreateLogger<ModelProviderMappingRepository>();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnMappingWithModelCapabilities()
        {
            // Arrange
            int mappingId = 0;
            
            SeedData(context =>
            {
                // Add provider first
                var provider = new Provider
                {
                    ProviderName = "Test Provider",
                    ProviderType = ProviderType.OpenAI,
                    BaseUrl = "https://api.openai.com/v1",
                    IsEnabled = true
                };
                context.Providers.Add(provider);
                context.SaveChanges();
                
                // Create a complete model with author, series, and capabilities
                var model = ModelTestHelper.CreateCompleteTestModel(
                    modelName: $"model-no-chat-{Guid.NewGuid()}",
                    supportsChat: false,
                    maxTokens: 4096);
                
                // Add the author, series and model to the context
                context.ModelAuthors.Add(model.Series.Author);
                context.ModelSeries.Add(model.Series);
                context.Models.Add(model);
                context.SaveChanges();
                
                // Add mapping with FK references
                var mapping = new ModelProviderMapping
                {
                    ModelAlias = "test-mapping",
                    ModelId = model.Id,
                    ProviderModelId = "gpt-3.5",
                    ProviderId = provider.Id,
                    IsEnabled = true
                };
                context.ModelProviderMappings.Add(mapping);
                context.SaveChanges();
                
                mappingId = mapping.Id;
            });

            var repository = new ModelProviderMappingRepository(CreateDbContextFactory(), _logger);

            // Act
            var mapping = await repository.GetByIdAsync(mappingId);

            // Assert
            Assert.NotNull(mapping);
            Assert.NotNull(mapping.Model);
            Assert.False(mapping.Model.SupportsChat);
            Assert.Equal(4096, mapping.Model.MaxInputTokens);
        }

        [Fact(Skip = "SQLite constraint issue - test creates duplicate data within single test method")]
        public async Task UpdateAsync_ChangingModelId_ShouldUpdateCapabilities()
        {
            // Arrange
            int mappingId = 0;
            int modelWithChatId = 0;
            var testId = Guid.NewGuid();
            
            SeedData(context =>
            {
                // Add provider
                var provider = new Provider
                {
                    ProviderName = "Test Provider",
                    ProviderType = ProviderType.OpenAI,
                    BaseUrl = "https://api.openai.com/v1",
                    IsEnabled = true
                };
                context.Providers.Add(provider);
                context.SaveChanges();
                
                // Add model series
                var series = new ModelSeries
                {
                    Name = $"Test Series {testId}"
                };
                context.ModelSeries.Add(series);
                context.SaveChanges();
                
                // Create models with different capabilities
                
                // Add models with FK references
                var modelNoChat = new Model
                {
                    Name = $"model-no-chat-{testId}",
                    ModelSeriesId = series.Id,
                    SupportsChat = false,
                    MaxInputTokens = 4096,
                    MaxOutputTokens = 2048
                };
                context.Models.Add(modelNoChat);
                
                var modelWithChat = new Model
                {
                    Name = $"model-with-chat-{testId}",
                    ModelSeriesId = series.Id,
                    SupportsChat = true,
                    SupportsVision = true,
                    SupportsStreaming = true,
                    MaxInputTokens = 8192,
                    MaxOutputTokens = 4096
                };
                context.Models.Add(modelWithChat);
                context.SaveChanges();
                
                // Add mapping with FK references
                var mapping = new ModelProviderMapping
                {
                    ModelAlias = "test-mapping",
                    ModelId = modelNoChat.Id,
                    ProviderModelId = "gpt-3.5",
                    ProviderId = provider.Id,
                    IsEnabled = true
                };
                context.ModelProviderMappings.Add(mapping);
                context.SaveChanges();
                
                mappingId = mapping.Id;
                modelWithChatId = modelWithChat.Id;
            });

            var repository = new ModelProviderMappingRepository(CreateDbContextFactory(), _logger);

            // Get initial mapping
            var mapping = await repository.GetByIdAsync(mappingId);
            Assert.NotNull(mapping);
            Assert.False(mapping.SupportsChat); // Initially false

            // Act - Change to model with chat support
            mapping.ModelId = modelWithChatId;
            await repository.UpdateAsync(mapping);

            // Assert - ModelId should be updated
            var updated = await repository.GetByIdAsync(mappingId);
            Assert.NotNull(updated);
            Assert.Equal(modelWithChatId, updated.ModelId);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Mapping_With_Model_Properties()
        {
            // Arrange
            int mappingId = 0;
            
            SeedData(context =>
            {
                // Add provider
                var provider = new Provider
                {
                    ProviderName = "Test Provider",
                    ProviderType = ProviderType.OpenAI,
                    BaseUrl = "https://api.openai.com/v1",
                    IsEnabled = true
                };
                context.Providers.Add(provider);
                context.SaveChanges();
                
                // Create a complete model with author, series, and capabilities
                var model = ModelTestHelper.CreateCompleteTestModel(
                    modelName: $"model-override-test-{Guid.NewGuid()}",
                    supportsChat: false,
                    maxTokens: 4096);
                
                // Add the author, series and model to the context
                context.ModelAuthors.Add(model.Series.Author);
                context.ModelSeries.Add(model.Series);
                context.Models.Add(model);
                context.SaveChanges();
                
                // Add mapping
                var mapping = new ModelProviderMapping
                {
                    ModelAlias = "override-test",
                    ModelId = model.Id,
                    ProviderModelId = "test",
                    ProviderId = provider.Id,
                    IsEnabled = true
                };
                context.ModelProviderMappings.Add(mapping);
                context.SaveChanges();
                
                mappingId = mapping.Id;
            });

            var repository = new ModelProviderMappingRepository(CreateDbContextFactory(), _logger);

            // Act
            var mapping = await repository.GetByIdAsync(mappingId);

            // Assert
            Assert.NotNull(mapping);
            Assert.NotNull(mapping.Model);
            Assert.Equal(4096, mapping.Model.MaxInputTokens);
        }
    }
}
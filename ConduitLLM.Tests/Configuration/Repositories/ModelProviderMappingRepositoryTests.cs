using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Enums;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    /// <summary>
    /// Unit tests for ModelProviderMappingRepository
    /// These tests would have caught the missing capability field updates in UpdateAsync
    /// </summary>
    public class ModelProviderMappingRepositoryTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly ModelProviderMappingRepository _repository;
        private readonly ILogger<ModelProviderMappingRepository> _logger;

        public ModelProviderMappingRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ConduitDbContext(options);
            _logger = new LoggerFactory().CreateLogger<ModelProviderMappingRepository>();

            var dbContextFactory = new TestDbContextFactory(options);
            _repository = new ModelProviderMappingRepository(dbContextFactory, _logger);

            // Seed test data
            SeedTestData();
        }

        private void SeedTestData()
        {
            // Add a test provider
            var provider = new Provider
            {
                Id = 1,
                ProviderName = "Test Provider",
                ProviderType = ProviderType.OpenAI,
                BaseUrl = "https://api.openai.com/v1",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Providers.Add(provider);

            // Add a test model mapping with all capabilities set to false initially
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "test-model",
                ProviderModelId = "gpt-3.5-turbo",
                ProviderId = 1,
                Provider = provider,
                IsEnabled = true,
                MaxContextTokens = 4096,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                // All capabilities initially false
                SupportsVision = false,
                SupportsAudioTranscription = false,
                SupportsTextToSpeech = false,
                SupportsRealtimeAudio = false,
                SupportsImageGeneration = false,
                SupportsVideoGeneration = false,
                SupportsEmbeddings = false,
                SupportsChat = false,  // This was the field that wasn't being updated!
                SupportsFunctionCalling = false,
                SupportsStreaming = false,
                IsDefault = false
            };

            _context.ModelProviderMappings.Add(mapping);
            _context.SaveChanges();
        }

        /// <summary>
        /// This test would have caught the SupportsChat bug!
        /// It verifies that ALL capability fields are properly updated in the repository
        /// </summary>
        [Fact]
        public async Task UpdateAsync_ShouldUpdateAllCapabilityFields()
        {
            // Arrange - Get the existing mapping
            var existingMapping = await _repository.GetByIdAsync(1);
            Assert.NotNull(existingMapping);

            // Verify initial state (all capabilities false)
            Assert.False(existingMapping.SupportsChat);
            Assert.False(existingMapping.SupportsVision);
            Assert.False(existingMapping.SupportsEmbeddings);
            Assert.False(existingMapping.SupportsFunctionCalling);
            Assert.False(existingMapping.SupportsStreaming);
            Assert.False(existingMapping.SupportsVideoGeneration);

            // Act - Update with all capabilities set to true
            existingMapping.SupportsVision = true;
            existingMapping.SupportsAudioTranscription = true;
            existingMapping.SupportsTextToSpeech = true;
            existingMapping.SupportsRealtimeAudio = true;
            existingMapping.SupportsImageGeneration = true;
            existingMapping.SupportsVideoGeneration = true;
            existingMapping.SupportsEmbeddings = true;
            existingMapping.SupportsChat = true;  // THE CRITICAL FIELD THAT WAS BROKEN!
            existingMapping.SupportsFunctionCalling = true;
            existingMapping.SupportsStreaming = true;

            var updateResult = await _repository.UpdateAsync(existingMapping);

            // Assert - Update succeeded
            Assert.True(updateResult);

            // Verify the updated values were saved to database by fetching fresh from DB
            var updatedMapping = await _repository.GetByIdAsync(1);
            Assert.NotNull(updatedMapping);

            // These assertions would have FAILED before the fix, catching the bug!
            Assert.True(updatedMapping.SupportsVision, "SupportsVision should be updated to true");
            Assert.True(updatedMapping.SupportsAudioTranscription, "SupportsAudioTranscription should be updated to true");
            Assert.True(updatedMapping.SupportsTextToSpeech, "SupportsTextToSpeech should be updated to true");
            Assert.True(updatedMapping.SupportsRealtimeAudio, "SupportsRealtimeAudio should be updated to true");
            Assert.True(updatedMapping.SupportsImageGeneration, "SupportsImageGeneration should be updated to true");
            Assert.True(updatedMapping.SupportsVideoGeneration, "SupportsVideoGeneration should be updated to true");
            Assert.True(updatedMapping.SupportsEmbeddings, "SupportsEmbeddings should be updated to true");
            Assert.True(updatedMapping.SupportsChat, "SupportsChat should be updated to true");  // THIS WOULD HAVE FAILED!
            Assert.True(updatedMapping.SupportsFunctionCalling, "SupportsFunctionCalling should be updated to true");
            Assert.True(updatedMapping.SupportsStreaming, "SupportsStreaming should be updated to true");

            // Verify UpdatedAt timestamp was updated
            Assert.True(updatedMapping.UpdatedAt > existingMapping.CreatedAt);
        }

        /// <summary>
        /// Test that individual capability fields can be toggled independently
        /// </summary>
        [Fact]
        public async Task UpdateAsync_ShouldToggleIndividualCapabilities()
        {
            // Arrange
            var existingMapping = await _repository.GetByIdAsync(1);
            Assert.NotNull(existingMapping);

            // Act - Toggle just the Chat capability
            existingMapping.SupportsChat = true;
            // Leave all other capabilities as false

            await _repository.UpdateAsync(existingMapping);

            // Assert - Only SupportsChat should be true
            var updatedMapping = await _repository.GetByIdAsync(1);
            Assert.NotNull(updatedMapping);

            Assert.True(updatedMapping.SupportsChat, "Only SupportsChat should be true");
            Assert.False(updatedMapping.SupportsVision, "SupportsVision should remain false");
            Assert.False(updatedMapping.SupportsEmbeddings, "SupportsEmbeddings should remain false");
            Assert.False(updatedMapping.SupportsFunctionCalling, "SupportsFunctionCalling should remain false");
            Assert.False(updatedMapping.SupportsStreaming, "SupportsStreaming should remain false");
        }

        /// <summary>
        /// Test that capability fields can be set from true back to false
        /// </summary>
        [Fact]
        public async Task UpdateAsync_ShouldAllowDisablingCapabilities()
        {
            // Arrange - First set capabilities to true
            var existingMapping = await _repository.GetByIdAsync(1);
            Assert.NotNull(existingMapping);

            existingMapping.SupportsChat = true;
            existingMapping.SupportsStreaming = true;
            await _repository.UpdateAsync(existingMapping);

            // Act - Now disable them
            existingMapping.SupportsChat = false;
            existingMapping.SupportsStreaming = false;
            await _repository.UpdateAsync(existingMapping);

            // Assert - Should be false again
            var updatedMapping = await _repository.GetByIdAsync(1);
            Assert.NotNull(updatedMapping);

            Assert.False(updatedMapping.SupportsChat, "SupportsChat should be disabled");
            Assert.False(updatedMapping.SupportsStreaming, "SupportsStreaming should be disabled");
        }

        /// <summary>
        /// Comprehensive test that verifies the exact scenario from the bug report
        /// </summary>
        [Fact]
        public async Task UpdateAsync_ChatCapabilityScenario_ShouldPersistCorrectly()
        {
            // Arrange - This replicates the exact bug scenario
            var existingMapping = await _repository.GetByIdAsync(1);
            Assert.NotNull(existingMapping);

            // Initial state: Chat is false, Streaming is true (like in the bug report)
            existingMapping.SupportsStreaming = true;
            await _repository.UpdateAsync(existingMapping);

            // Verify initial state
            var initialCheck = await _repository.GetByIdAsync(1);
            Assert.False(initialCheck.SupportsChat, "Chat should initially be false");
            Assert.True(initialCheck.SupportsStreaming, "Streaming should be true");

            // Act - User enables Chat capability (this was failing before the fix)
            existingMapping.SupportsChat = true;
            var updateResult = await _repository.UpdateAsync(existingMapping);

            // Assert - This is the critical test that would have failed
            Assert.True(updateResult, "Update should succeed");

            var finalCheck = await _repository.GetByIdAsync(1);
            Assert.NotNull(finalCheck);
            Assert.True(finalCheck.SupportsChat, "Chat capability should now be enabled - THIS WAS THE BUG!");
            Assert.True(finalCheck.SupportsStreaming, "Streaming should still be enabled");
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }

    /// <summary>
    /// Test helper for creating DbContext instances
    /// </summary>
    internal class TestDbContextFactory : IDbContextFactory<ConduitDbContext>
    {
        private readonly DbContextOptions<ConduitDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ConduitDbContext> options)
        {
            _options = options;
        }

        public ConduitDbContext CreateDbContext()
        {
            return new ConduitDbContext(_options);
        }

        public Task<ConduitDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ConduitDbContext(_options));
        }
    }
}
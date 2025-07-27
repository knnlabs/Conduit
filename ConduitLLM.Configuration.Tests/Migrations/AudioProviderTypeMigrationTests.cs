using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ConduitLLM.Configuration.Tests.Migrations
{
    /// <summary>
    /// Tests for the AudioProviderType migration to ensure entities work correctly with ProviderType enum.
    /// </summary>
    public class AudioProviderTypeMigrationTests : IDisposable
    {
        private readonly ConfigurationDbContext _context;
        private readonly AudioCostRepository _costRepository;
        private readonly AudioUsageLogRepository _usageRepository;

        public AudioProviderTypeMigrationTests()
        {
            var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ConfigurationDbContext(options);
            _costRepository = new AudioCostRepository(_context);
            _usageRepository = new AudioUsageLogRepository(_context);
        }

        [Fact]
        public async Task AudioCost_Should_Store_And_Retrieve_ProviderType()
        {
            // Arrange
            var cost = new AudioCost
            {
                Provider = ProviderType.OpenAI,
                OperationType = "transcription",
                Model = "whisper-1",
                CostUnit = "minute",
                CostPerUnit = 0.006m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            };

            // Act
            var created = await _costRepository.CreateAsync(cost);
            var retrieved = await _costRepository.GetByIdAsync(created.Id);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(ProviderType.OpenAI, retrieved.Provider);
            Assert.Equal("transcription", retrieved.OperationType);
        }

        [Fact]
        public async Task AudioUsageLog_Should_Store_And_Retrieve_ProviderType()
        {
            // Arrange
            var usageLog = new AudioUsageLog
            {
                VirtualKey = "test-key-123",
                Provider = ProviderType.ElevenLabs,
                OperationType = "tts",
                Model = "eleven_monolingual_v1",
                CharacterCount = 1000,
                Cost = 0.18m,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };

            // Act
            _context.AudioUsageLogs.Add(usageLog);
            await _context.SaveChangesAsync();

            var retrieved = await _context.AudioUsageLogs
                .FirstOrDefaultAsync(l => l.VirtualKey == "test-key-123");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(ProviderType.ElevenLabs, retrieved.Provider);
            Assert.Equal("tts", retrieved.OperationType);
            Assert.Equal(0.18m, retrieved.Cost);
        }

        [Fact]
        public async Task Repository_Should_Query_By_ProviderType()
        {
            // Arrange
            var costs = new[]
            {
                new AudioCost
                {
                    Provider = ProviderType.OpenAI,
                    OperationType = "transcription",
                    Model = "whisper-1",
                    CostUnit = "minute",
                    CostPerUnit = 0.006m,
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow
                },
                new AudioCost
                {
                    Provider = ProviderType.GoogleCloud,
                    OperationType = "transcription",
                    Model = "default",
                    CostUnit = "minute",
                    CostPerUnit = 0.016m,
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow
                },
                new AudioCost
                {
                    Provider = ProviderType.OpenAI,
                    OperationType = "tts",
                    Model = "tts-1",
                    CostUnit = "character",
                    CostPerUnit = 0.000015m,
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow
                }
            };

            foreach (var cost in costs)
            {
                await _costRepository.CreateAsync(cost);
            }

            // Act
            var openAiCosts = await _costRepository.GetByProviderAsync(ProviderType.OpenAI);
            var googleCosts = await _costRepository.GetByProviderAsync(ProviderType.GoogleCloud);

            // Assert
            Assert.Equal(2, openAiCosts.Count);
            Assert.Single(googleCosts);
            Assert.All(openAiCosts, c => Assert.Equal(ProviderType.OpenAI, c.Provider));
            Assert.All(googleCosts, c => Assert.Equal(ProviderType.GoogleCloud, c.Provider));
        }

        [Fact]
        public async Task GetCurrentCost_Should_Work_With_ProviderType()
        {
            // Arrange
            var cost = new AudioCost
            {
                Provider = ProviderType.AWSTranscribe,
                OperationType = "transcription",
                Model = "standard",
                CostUnit = "second",
                CostPerUnit = 0.00040m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                EffectiveTo = null
            };

            await _costRepository.CreateAsync(cost);

            // Act
            var currentCost = await _costRepository.GetCurrentCostAsync(
                ProviderType.AWSTranscribe, 
                "transcription", 
                "standard"
            );

            // Assert
            Assert.NotNull(currentCost);
            Assert.Equal(ProviderType.AWSTranscribe, currentCost.Provider);
            Assert.Equal(0.00040m, currentCost.CostPerUnit);
        }

        [Theory]
        [InlineData(ProviderType.OpenAI)]
        [InlineData(ProviderType.ElevenLabs)]
        [InlineData(ProviderType.GoogleCloud)]
        [InlineData(ProviderType.AWSTranscribe)]
        public async Task All_Audio_Providers_Should_Be_Supported(ProviderType providerType)
        {
            // Arrange
            var cost = new AudioCost
            {
                Provider = providerType,
                OperationType = "test",
                Model = "test-model",
                CostUnit = "unit",
                CostPerUnit = 0.01m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            };

            // Act
            var created = await _costRepository.CreateAsync(cost);
            var retrieved = await _costRepository.GetByIdAsync(created.Id);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(providerType, retrieved.Provider);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
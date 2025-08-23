using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Tests.Configuration.Migrations
{
    /// <summary>
    /// Tests for the AudioProviderType migration to ensure entities work correctly with ProviderType enum.
    /// </summary>
    public class AudioProviderTypeMigrationTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly AudioCostRepository _costRepository;
        private readonly AudioUsageLogRepository _usageRepository;

        public AudioProviderTypeMigrationTests()
        {
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ConduitDbContext(options);
            _context.IsTestEnvironment = true;
            _costRepository = new AudioCostRepository(_context);
            _usageRepository = new AudioUsageLogRepository(_context);
        }

        [Fact]
        public async Task AudioCost_Should_Store_And_Retrieve_ProviderType()
        {
            // Arrange
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            var cost = new AudioCost
            {
                ProviderId = provider.Id,
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
            Assert.Equal(provider.Id, retrieved.ProviderId);
            Assert.Equal("transcription", retrieved.OperationType);
        }

        [Fact]
        public async Task AudioUsageLog_Should_Store_And_Retrieve_ProviderType()
        {
            // Arrange
            var provider = new Provider { ProviderType = ProviderType.ElevenLabs, ProviderName = "ElevenLabs" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            var usageLog = new AudioUsageLog
            {
                VirtualKey = "test-key-123",
                ProviderId = provider.Id,
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
            Assert.Equal(provider.Id, retrieved.ProviderId);
            Assert.Equal("tts", retrieved.OperationType);
            Assert.Equal(0.18m, retrieved.Cost);
        }

        [Fact]
        public async Task Repository_Should_Query_By_ProviderType()
        {
            // Arrange
            var openAiProvider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            var googleProvider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "Google Cloud" };
            _context.Providers.AddRange(openAiProvider, googleProvider);
            await _context.SaveChangesAsync();

            var costs = new[]
            {
                new AudioCost
                {
                    ProviderId = openAiProvider.Id,
                    OperationType = "transcription",
                    Model = "whisper-1",
                    CostUnit = "minute",
                    CostPerUnit = 0.006m,
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow
                },
                new AudioCost
                {
                    ProviderId = googleProvider.Id,
                    OperationType = "transcription",
                    Model = "default",
                    CostUnit = "minute",
                    CostPerUnit = 0.016m,
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow
                },
                new AudioCost
                {
                    ProviderId = openAiProvider.Id,
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
            var openAiCosts = await _costRepository.GetByProviderAsync(openAiProvider.Id);
            var googleCosts = await _costRepository.GetByProviderAsync(googleProvider.Id);

            // Assert
            Assert.Equal(2, openAiCosts.Count);
            Assert.Single(googleCosts);
            Assert.All(openAiCosts, c => Assert.Equal(openAiProvider.Id, c.ProviderId));
            Assert.All(googleCosts, c => Assert.Equal(googleProvider.Id, c.ProviderId));
        }

        [Fact]
        public async Task GetCurrentCost_Should_Work_With_ProviderType()
        {
            // Arrange
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "AWS Transcribe" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            var cost = new AudioCost
            {
                ProviderId = provider.Id,
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
                provider.Id, 
                "transcription", 
                "standard"
            );

            // Assert
            Assert.NotNull(currentCost);
            Assert.Equal(provider.Id, currentCost.ProviderId);
            Assert.Equal(0.00040m, currentCost.CostPerUnit);
        }

        [Theory]
        [InlineData(ProviderType.OpenAI)]
        [InlineData(ProviderType.ElevenLabs)]
        public async Task All_Audio_Providers_Should_Be_Supported(ProviderType providerType)
        {
            // Arrange
            var provider = new Provider { ProviderType = providerType, ProviderName = providerType.ToString() };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            var cost = new AudioCost
            {
                ProviderId = provider.Id,
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
            Assert.Equal(provider.Id, retrieved.ProviderId);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
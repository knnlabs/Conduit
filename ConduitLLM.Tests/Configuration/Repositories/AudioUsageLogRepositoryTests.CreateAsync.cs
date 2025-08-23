using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

using FluentAssertions;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class AudioUsageLogRepositoryTests
    {
        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_WithValidLog_ShouldPersistLog()
        {
            // Arrange
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            var log = new AudioUsageLog
            {
                VirtualKey = "test-key-hash",
                ProviderId = provider.Id,
                OperationType = "transcription",
                Model = "whisper-1",
                RequestId = Guid.NewGuid().ToString(),
                DurationSeconds = 15.5,
                CharacterCount = 1000,
                Cost = 0.15m,
                Language = "en",
                StatusCode = 200
            };

            // Act
            var result = await _repository.CreateAsync(log);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            var savedLog = await _context.AudioUsageLogs.FindAsync(result.Id);
            savedLog.Should().NotBeNull();
            savedLog!.VirtualKey.Should().Be("test-key-hash");
            savedLog.ProviderId.Should().Be(provider.Id);
        }

        [Fact]
        public async Task CreateAsync_WithErrorLog_ShouldPersistWithError()
        {
            // Arrange
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "Azure OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            var log = new AudioUsageLog
            {
                VirtualKey = "test-key-hash",
                ProviderId = provider.Id,
                OperationType = "tts",
                Model = "tts-1",
                RequestId = Guid.NewGuid().ToString(),
                StatusCode = 500,
                ErrorMessage = "Internal server error",
                Cost = 0m
            };

            // Act
            var result = await _repository.CreateAsync(log);

            // Assert
            result.StatusCode.Should().Be(500);
            result.ErrorMessage.Should().Be("Internal server error");
            result.Cost.Should().Be(0);
        }

        #endregion
    }
}
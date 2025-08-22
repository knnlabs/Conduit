using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class AudioUsageLogRepositoryTests
    {
        #region Helper Methods

        private async Task SeedTestDataAsync(int count, int maxDaysAgo = 30)
        {
            // Create test providers
            var openAiProvider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            var azureProvider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "Azure OpenAI" };
            _context.Providers.AddRange(openAiProvider, azureProvider);
            await _context.SaveChangesAsync();

            var logs = new List<AudioUsageLog>();
            var random = new Random(42); // Use fixed seed for deterministic behavior
            
            for (int i = 0; i < count; i++)
            {
                var operationType = i % 3 == 0 ? "transcription" : i % 3 == 1 ? "tts" : "realtime";
                var provider = i % 2 == 0 ? openAiProvider : azureProvider;
                var statusCode = i % 10 == 0 ? 500 : 200;
                
                // Distribute timestamps evenly across the time range to ensure all operation types appear in any window
                var daysAgo = (i * maxDaysAgo) / count;
                
                logs.Add(new AudioUsageLog
                {
                    VirtualKey = $"key-{i % 3}",
                    ProviderId = provider.Id,
                    OperationType = operationType,
                    Model = provider.ProviderType == ProviderType.OpenAI ? "whisper-1" : "azure-tts",
                    RequestId = Guid.NewGuid().ToString(),
                    SessionId = operationType == "realtime" ? Guid.NewGuid().ToString() : null,
                    DurationSeconds = random.Next(1, 60),
                    CharacterCount = random.Next(100, 5000),
                    Cost = (decimal)(random.NextDouble() * 2),
                    Language = "en",
                    Voice = operationType == "tts" ? "alloy" : null,
                    StatusCode = statusCode,
                    ErrorMessage = statusCode >= 400 ? "Error occurred" : null,
                    IpAddress = $"192.168.1.{i % 255}",
                    UserAgent = "Test/1.0",
                    Timestamp = DateTime.UtcNow.AddDays(-daysAgo)
                });
            }

            _context.AudioUsageLogs.AddRange(logs);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}
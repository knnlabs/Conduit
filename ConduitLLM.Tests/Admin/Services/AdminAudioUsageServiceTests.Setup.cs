using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminAudioUsageServiceTests
    {
        private readonly Mock<IAudioUsageLogRepository> _mockRepository;
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly Mock<ILogger<AdminAudioUsageService>> _mockLogger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IRealtimeSessionStore> _mockSessionStore;
        private readonly Mock<ConduitLLM.Core.Interfaces.ICostCalculationService> _mockCostCalculationService;
        private readonly AdminAudioUsageService _service;
        private readonly ITestOutputHelper _output;

        public AdminAudioUsageServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockRepository = new Mock<IAudioUsageLogRepository>();
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockLogger = new Mock<ILogger<AdminAudioUsageService>>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockSessionStore = new Mock<IRealtimeSessionStore>();
            _mockCostCalculationService = new Mock<ConduitLLM.Core.Interfaces.ICostCalculationService>();
            
            // Setup service provider to return session store
            var mockScope = new Mock<IServiceScope>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScopedProvider = new Mock<IServiceProvider>();
            
            mockScope.Setup(x => x.ServiceProvider).Returns(mockScopedProvider.Object);
            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);
            mockScopedProvider.Setup(x => x.GetService(typeof(IRealtimeSessionStore))).Returns(_mockSessionStore.Object);

            _service = new AdminAudioUsageService(
                _mockRepository.Object,
                _mockVirtualKeyRepository.Object,
                _mockLogger.Object,
                _mockServiceProvider.Object,
                _mockCostCalculationService.Object);
        }

        #region Helper Methods

        private List<AudioUsageLog> CreateSampleAudioUsageLogs(int count)
        {
            var logs = new List<AudioUsageLog>();
            for (int i = 0; i < count; i++)
            {
                logs.Add(new AudioUsageLog
                {
                    Id = i + 1,
                    VirtualKey = $"key-{i % 3}",
                    ProviderId = i % 2 == 0 ? 1 : 2, // Alternate between provider 1 and 2
                    OperationType = i % 3 == 0 ? "transcription" : i % 3 == 1 ? "tts" : "realtime",
                    Model = i % 2 == 0 ? "whisper-1" : "tts-1",
                    RequestId = Guid.NewGuid().ToString(),
                    DurationSeconds = 10 + i,
                    Cost = 0.05m + (i * 0.01m),
                    StatusCode = i % 10 == 0 ? 500 : 200,
                    Timestamp = DateTime.UtcNow.AddHours(-i)
                });
            }
            return logs;
        }

        private AudioUsageLog CreateAudioUsageLog(string operationType, string model, int statusCode)
        {
            return new AudioUsageLog
            {
                Id = 1,
                VirtualKey = "test-key",
                ProviderId = 1, // Provider ID 1 for OpenAI
                OperationType = operationType,
                Model = model,
                RequestId = Guid.NewGuid().ToString(),
                DurationSeconds = 5,
                Cost = 0.10m,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };
        }

        private List<RealtimeSession> CreateSampleRealtimeSessions(int count)
        {
            var sessions = new List<RealtimeSession>();
            for (int i = 0; i < count; i++)
            {
                sessions.Add(CreateRealtimeSession($"session-{i + 1}", i % 3 == 0 ? "ultravox" : "openai", i == 0));
            }
            return sessions;
        }

        private RealtimeSession CreateRealtimeSession(string sessionId, string provider, bool hasErrors = false)
        {
            var config = new RealtimeSessionConfig
            {
                Model = provider == "openai" ? "gpt-4o-realtime" : "ultravox-v0.2",
                Voice = "alloy",
                Language = "en-US"
            };

            // Map provider names to ProviderType IDs
            var providerId = provider.ToLowerInvariant() switch
            {
                "openai" => 1,      // ProviderType.OpenAI
                "ultravox" => 18,   // ProviderType.Ultravox
                _ => 1              // Default to OpenAI
            };

            var session = new RealtimeSession
            {
                Id = sessionId,
                Provider = provider,
                Config = config,
                State = SessionState.Connected,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                Metadata = new Dictionary<string, object>
                {
                    { "VirtualKey", "test-key-hash" },
                    { "IpAddress", "192.168.1.1" },
                    { "UserAgent", "Mozilla/5.0" },
                    { "ProviderId", providerId }
                }
            };

            session.Statistics.Duration = TimeSpan.FromMinutes(25);
            session.Statistics.TurnCount = 10;
            session.Statistics.InputTokens = 1000;
            session.Statistics.OutputTokens = 2000;
            session.Statistics.InputAudioDuration = TimeSpan.FromMinutes(5);
            session.Statistics.OutputAudioDuration = TimeSpan.FromMinutes(10);
            session.Statistics.ErrorCount = hasErrors ? 2 : 0;

            return session;
        }

        #endregion
    }
}
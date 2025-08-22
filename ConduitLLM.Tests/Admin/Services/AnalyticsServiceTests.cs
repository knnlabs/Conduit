using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AnalyticsServiceTests
    {
        private readonly Mock<IRequestLogRepository> _mockRequestLogRepository;
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<ILogger<AnalyticsService>> _mockLogger;
        private readonly AnalyticsService _service;

        public AnalyticsServiceTests()
        {
            _mockRequestLogRepository = new Mock<IRequestLogRepository>();
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _mockLogger = new Mock<ILogger<AnalyticsService>>();
            
            _service = new AnalyticsService(
                _mockRequestLogRepository.Object,
                _mockVirtualKeyRepository.Object,
                _memoryCache,
                _mockLogger.Object
            );
        }

        #region Helper Methods

        private List<RequestLog> GenerateTestLogs(int count)
        {
            var logs = new List<RequestLog>();
            var models = new[] { "gpt-4", "gpt-3.5-turbo", "claude-3" };
            var random = new Random();
            
            for (int i = 0; i < count; i++)
            {
                logs.Add(new RequestLog
                {
                    Id = i + 1,
                    VirtualKeyId = random.Next(1, 5),
                    ModelName = models[random.Next(models.Length)],
                    RequestType = "chat",
                    InputTokens = random.Next(50, 500),
                    OutputTokens = random.Next(50, 500),
                    Cost = (decimal)(random.NextDouble() * 0.1),
                    ResponseTimeMs = random.Next(500, 3000),
                    StatusCode = random.Next(10) > 8 ? 429 : 200,
                    Timestamp = DateTime.UtcNow.AddDays(-random.Next(30)),
                    UserId = $"user{random.Next(1, 10)}",
                    ClientIp = $"192.168.1.{random.Next(1, 255)}",
                    RequestPath = "/v1/chat/completions"
                });
            }
            
            return logs;
        }

        #endregion
    }
}
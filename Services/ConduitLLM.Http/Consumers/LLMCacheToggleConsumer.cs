using MassTransit;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Consumes LLMCacheToggleEvent to enable/disable LLM caching at runtime.
    /// Updates the in-memory configuration which is monitored by CachingLLMClient via IOptionsMonitor.
    /// </summary>
    public class LLMCacheToggleConsumer : IConsumer<LLMCacheToggleEvent>
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LLMCacheToggleConsumer> _logger;

        public LLMCacheToggleConsumer(
            IConfiguration configuration,
            ILogger<LLMCacheToggleConsumer> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task Consume(ConsumeContext<LLMCacheToggleEvent> context)
        {
            var evt = context.Message;

            _logger.LogWarning(
                "LLM caching {Action} by {User} at {Time}. Reason: {Reason}",
                evt.Enabled ? "ENABLED" : "DISABLED",
                evt.ToggledBy,
                evt.ToggledAt,
                evt.Reason ?? "None provided");

            // Update the in-memory configuration
            // This uses IConfiguration's built-in change notification system
            // which IOptionsMonitor subscribes to
            _configuration["Cache:LLMCachingEnabled"] = evt.Enabled.ToString();

            _logger.LogInformation(
                "LLM caching runtime configuration updated to: {Enabled}",
                evt.Enabled);

            return Task.CompletedTask;
        }
    }
}

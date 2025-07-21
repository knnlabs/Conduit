using System;
using System.Collections.Generic;
using System.Linq;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Classifies model discovery notifications by severity
    /// </summary>
    public interface INotificationSeverityClassifier
    {
        NotificationSeverity ClassifyNewModel(string provider, DiscoveredModelInfo model);
        NotificationSeverity ClassifyCapabilityChange(string provider, string modelId, List<string> changes);
        NotificationSeverity ClassifyPriceChange(string provider, string modelId, decimal percentageChange);
        NotificationSeverity ClassifyProviderEvent(string provider, string eventType);
    }

    public class NotificationSeverityClassifier : INotificationSeverityClassifier
    {
        private readonly HashSet<string> _majorProviders = new(StringComparer.OrdinalIgnoreCase)
        {
            "openai", "anthropic", "google", "microsoft", "aws", "cohere", "meta"
        };

        private readonly HashSet<string> _criticalCapabilities = new(StringComparer.OrdinalIgnoreCase)
        {
            "vision", "embeddings", "function calling", "function_calling", "video generation", "video_generation"
        };

        public NotificationSeverity ClassifyNewModel(string provider, DiscoveredModelInfo model)
        {
            // New models from major providers are high priority
            if (_majorProviders.Contains(provider))
                return NotificationSeverity.High;

            // Models with multiple advanced capabilities are high priority
            var advancedCapabilityCount = CountAdvancedCapabilities(model.Capabilities);
            if (advancedCapabilityCount >= 2)
                return NotificationSeverity.High;

            // Single advanced capability is medium
            if (advancedCapabilityCount == 1)
                return NotificationSeverity.Medium;

            // Basic models are low priority
            return NotificationSeverity.Low;
        }

        public NotificationSeverity ClassifyCapabilityChange(string provider, string modelId, List<string> changes)
        {
            // Major capability additions are high priority
            var addedCapabilities = changes
                .Where(c => c.Contains("→ True", StringComparison.OrdinalIgnoreCase))
                .Count(c => 
                {
                    var capabilityName = c.Split(':')[0].Trim();
                    // Check if any critical capability matches (case-insensitive)
                    return _criticalCapabilities.Any(cap => 
                        string.Equals(cap, capabilityName, StringComparison.OrdinalIgnoreCase));
                });

            if (addedCapabilities > 0)
                return NotificationSeverity.High;

            // Capability removals are medium priority
            var removedCapabilities = changes
                .Where(c => c.Contains("→ False", StringComparison.OrdinalIgnoreCase))
                .Any();

            if (removedCapabilities)
                return NotificationSeverity.Medium;

            // Other changes are low priority
            return NotificationSeverity.Low;
        }

        public NotificationSeverity ClassifyPriceChange(string provider, string modelId, decimal percentageChange)
        {
            var absChange = Math.Abs(percentageChange);

            // Price changes > 50% are high priority
            if (absChange > 50)
                return NotificationSeverity.High;

            // Price changes > 10% are medium priority
            if (absChange > 10)
                return NotificationSeverity.Medium;

            // Small price changes are low priority
            return NotificationSeverity.Low;
        }

        public NotificationSeverity ClassifyProviderEvent(string provider, string eventType)
        {
            switch (eventType.ToLowerInvariant())
            {
                case "provider_offline":
                case "provider_error":
                case "authentication_failed":
                    return NotificationSeverity.Critical;
                
                case "provider_online":
                case "new_provider":
                    return NotificationSeverity.High;
                
                case "provider_updated":
                case "rate_limit_changed":
                    return NotificationSeverity.Medium;
                
                default:
                    return NotificationSeverity.Low;
            }
        }

        private int CountAdvancedCapabilities(ModelCapabilityInfo capabilities)
        {
            int count = 0;
            
            if (capabilities.Vision) count++;
            if (capabilities.Embeddings) count++;
            if (capabilities.FunctionCalling) count++;
            if (capabilities.VideoGeneration) count++;
            if (capabilities.AudioTranscription) count++;
            if (capabilities.TextToSpeech) count++;
            
            return count;
        }
    }
}
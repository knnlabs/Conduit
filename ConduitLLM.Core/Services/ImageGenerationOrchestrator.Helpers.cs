using ConduitLLM.Core.Events;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Image generation orchestrator - Helper methods
    /// </summary>
    public partial class ImageGenerationOrchestrator
    {
        private async Task<ModelInfo?> GetModelInfoAsync(string? requestedModel, string virtualKeyHash)
        {
            // Get virtual key to check model access
            var virtualKey = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKeyHash, requestedModel);
            if (virtualKey == null)
            {
                return null;
            }
            
            // If no model specified, use default image model
            if (string.IsNullOrEmpty(requestedModel))
            {
                // Would need to implement default image model selection
                requestedModel = "dall-e-3"; // Default to DALL-E 3
            }
            
            // Get model mapping
            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(requestedModel);
            if (mapping == null)
            {
                return null;
            }
            
            // Verify model supports image generation
            if (!mapping.SupportsImageGeneration)
            {
                _logger.LogWarning("Model {Model} does not support image generation", requestedModel);
                return null;
            }
            
            // Get the provider entity
            var provider = await _providerService.GetProviderByIdAsync(mapping.ProviderId);
            if (provider == null)
            {
                _logger.LogWarning("Provider not found for ProviderId {ProviderId}", mapping.ProviderId);
                return null;
            }
            
            return new ModelInfo
            {
                Provider = provider,
                ModelId = mapping.ProviderModelId,
                ProviderId = mapping.ProviderId
            };
        }

        private async Task<decimal> CalculateImageGenerationCostAsync(ProviderType providerType, string model, int imageCount, CancellationToken cancellationToken)
        {
            // Create usage object for cost calculation
            var usage = new Usage
            {
                ImageCount = imageCount
            };
            
            // Use the centralized cost calculation service
            var cost = await _costCalculationService.CalculateCostAsync(model, usage, cancellationToken);
            
            return cost;
        }

        private bool IsRetryableError(Exception ex)
        {
            // Determine if error is retryable
            return ex switch
            {
                TaskCanceledException => true,
                TimeoutException => true,
                _ when ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
                _ when ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) => true,
                _ when ex.Message.Contains("temporary", StringComparison.OrdinalIgnoreCase) => true,
                _ => false
            };
        }

        private async Task ReportProgressAsync(
            string taskId,
            string correlationId,
            int totalImages,
            Func<int> getCompletedCount,
            string? webhookUrl,
            Dictionary<string, string>? webhookHeaders,
            CancellationToken cancellationToken)
        {
            var lastReportedCount = 0;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                
                var currentCount = getCompletedCount();
                if (currentCount != lastReportedCount)
                {
                    lastReportedCount = currentCount;
                    
                    await _publishEndpoint.Publish(new ImageGenerationProgress
                    {
                        TaskId = taskId,
                        Status = "storing",
                        ImagesCompleted = currentCount,
                        TotalImages = totalImages,
                        Message = $"Processed {currentCount} of {totalImages} images",
                        CorrelationId = correlationId ?? string.Empty
                    });
                    
                    // Send webhook notification if configured
                    if (!string.IsNullOrEmpty(webhookUrl))
                    {
                        var webhookPayload = new ImageProgressWebhookPayload
                        {
                            TaskId = taskId,
                            Status = "processing",
                            ImagesCompleted = currentCount,
                            TotalImages = totalImages,
                            Message = $"Processed {currentCount} of {totalImages} images"
                        };
                        
                        // Publish webhook delivery event for scalable processing
                        await _publishEndpoint.Publish(new WebhookDeliveryRequested
                        {
                            TaskId = taskId,
                            TaskType = "image",
                            WebhookUrl = webhookUrl,
                            EventType = WebhookEventType.TaskProgress,
                            PayloadJson = ConduitLLM.Core.Helpers.WebhookPayloadHelper.SerializePayload(webhookPayload),
                            Headers = webhookHeaders,
                            CorrelationId = correlationId ?? Guid.NewGuid().ToString()
                        });
                    }
                }
                
                if (currentCount >= totalImages)
                {
                    break;
                }
            }
        }

        private int GetOptimalConcurrency(string provider, int imageCount)
        {
            // Use configuration or fallback to defaults
            var maxConcurrency = _performanceConfig.ProviderConcurrencyLimits.TryGetValue(
                provider.ToLowerInvariant(), 
                out var limit) ? limit : _performanceConfig.MaxConcurrentGenerations;
            
            // Don't exceed the number of images
            return Math.Min(maxConcurrency, imageCount);
        }

        private TimeSpan GetProviderTimeout(ProviderType providerType)
        {
            // Use configuration or fallback to defaults
            var providerKey = providerType.ToString().ToLowerInvariant();
            var timeoutSeconds = _performanceConfig.ProviderDownloadTimeouts.TryGetValue(
                providerKey, 
                out var timeout) ? timeout : 30;
            
            return TimeSpan.FromSeconds(timeoutSeconds);
        }

        private class ModelInfo
        {
            public ConduitLLM.Configuration.Entities.Provider? Provider { get; set; }
            public string ModelId { get; set; } = string.Empty;
            public int ProviderId { get; set; }
            
            // Convenience property to get ProviderType from Provider
            public ProviderType ProviderType => Provider?.ProviderType ?? ProviderType.OpenAI;
            
            // Convenience property to get provider name for responses
            public string ProviderName => Provider?.ProviderName ?? Provider?.ProviderType.ToString() ?? "unknown";
        }
    }
}
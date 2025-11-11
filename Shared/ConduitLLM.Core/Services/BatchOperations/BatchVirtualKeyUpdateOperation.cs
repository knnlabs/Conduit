using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
namespace ConduitLLM.Core.Services.BatchOperations
{
    /// <summary>
    /// Batch operation for updating multiple virtual keys (budgets, models, settings)
    /// </summary>
    public class BatchVirtualKeyUpdateOperation : IBatchVirtualKeyUpdateOperation
    {
        private readonly ILogger<BatchVirtualKeyUpdateOperation> _logger;
        private readonly IBatchOperationService _batchOperationService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ISystemNotificationService _notificationService;

        public BatchVirtualKeyUpdateOperation(
            ILogger<BatchVirtualKeyUpdateOperation> logger,
            IBatchOperationService batchOperationService,
            IVirtualKeyService virtualKeyService,
            ISystemNotificationService notificationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchOperationService = batchOperationService ?? throw new ArgumentNullException(nameof(batchOperationService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// Execute batch virtual key update operation
        /// </summary>
        public async Task<BatchOperationResult> ExecuteAsync(
            List<VirtualKeyUpdateItem> updates,
            int adminVirtualKeyId,
            CancellationToken cancellationToken = default)
        {
            var options = new BatchOperationOptions
            {
                VirtualKeyId = adminVirtualKeyId,
                MaxDegreeOfParallelism = 5, // Limit parallelism for database operations
                ContinueOnError = true,
                EnableCheckpointing = true,
                CheckpointInterval = 25,
                Metadata = new Dictionary<string, object>
                {
                    ["updateType"] = "virtual_key_batch_update",
                    ["adminKeyId"] = adminVirtualKeyId
                }
            };

            return await _batchOperationService.StartBatchOperationAsync(
                "virtual_key_update",
                updates,
                ProcessVirtualKeyUpdateAsync,
                options,
                cancellationToken);
        }

        private async Task<BatchItemResult> ProcessVirtualKeyUpdateAsync(
            VirtualKeyUpdateItem item,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Get existing virtual key
                var virtualKeyInfo = await _virtualKeyService.GetVirtualKeyInfoAsync(item.VirtualKeyId);
                if (virtualKeyInfo == null)
                {
                    return new BatchItemResult
                    {
                        Success = false,
                        ItemIdentifier = $"VKey-{item.VirtualKeyId}",
                        Error = "Virtual key not found",
                        Duration = stopwatch.Elapsed
                    };
                }

                var changedProperties = new List<string>();

                // Build update request
                var updateRequest = new ConduitLLM.Configuration.DTOs.VirtualKey.UpdateVirtualKeyRequestDto
                {
                    KeyName = virtualKeyInfo.KeyName,
                    IsEnabled = item.IsEnabled ?? virtualKeyInfo.IsEnabled,
                    ExpiresAt = item.ExpiresAt ?? virtualKeyInfo.ExpiresAt,
                    // Convert List<string> to comma-separated string
                    AllowedModels = item.AllowedModels != null 
                        ? string.Join(",", item.AllowedModels) 
                        : virtualKeyInfo.AllowedModels,
                    // Handle rate limits
                    RateLimitRpm = item.RateLimits?.ContainsKey("rpm") == true 
                        ? Convert.ToInt32(item.RateLimits["rpm"]) 
                        : virtualKeyInfo.RateLimitRpm,
                    RateLimitRpd = item.RateLimits?.ContainsKey("rpd") == true 
                        ? Convert.ToInt32(item.RateLimits["rpd"]) 
                        : virtualKeyInfo.RateLimitRpd
                };

                // Track changes - MaxBudget removed as it's now at group level

                if (item.AllowedModels != null)
                {
                    changedProperties.Add($"AllowedModels: {item.AllowedModels.Count} models");
                }

                if (item.RateLimits != null)
                {
                    changedProperties.Add("RateLimits: Updated");
                }

                if (item.IsEnabled.HasValue && virtualKeyInfo.IsEnabled != item.IsEnabled.Value)
                {
                    changedProperties.Add($"IsEnabled: {item.IsEnabled.Value}");
                }

                if (item.ExpiresAt.HasValue)
                {
                    changedProperties.Add($"ExpiresAt: {item.ExpiresAt.Value:yyyy-MM-dd}");
                }

                if (changedProperties.Count() > 0)
                {
                    // Save changes
                    var updated = await _virtualKeyService.UpdateVirtualKeyAsync(item.VirtualKeyId, updateRequest);
                    
                    if (!updated)
                    {
                        return new BatchItemResult
                        {
                            Success = false,
                            ItemIdentifier = $"VKey-{item.VirtualKeyId}",
                            Error = "Failed to update virtual key",
                            Duration = stopwatch.Elapsed
                        };
                    }

                    // Send notification
                    await _notificationService.NotifyConfigurationChangedAsync(
                        item.VirtualKeyId,
                        "VirtualKeySettings",
                        changedProperties);

                    return new BatchItemResult
                    {
                        Success = true,
                        ItemIdentifier = $"VKey-{item.VirtualKeyId}",
                        Duration = stopwatch.Elapsed,
                        Data = new
                        {
                            VirtualKeyId = item.VirtualKeyId,
                            ChangedProperties = changedProperties
                        }
                    };
                }
                else
                {
                    return new BatchItemResult
                    {
                        Success = true,
                        ItemIdentifier = $"VKey-{item.VirtualKeyId}",
                        Duration = stopwatch.Elapsed,
                        Data = new { VirtualKeyId = item.VirtualKeyId, Message = "No changes required" }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to update virtual key {VirtualKeyId}", 
                    item.VirtualKeyId);

                return new BatchItemResult
                {
                    Success = false,
                    ItemIdentifier = $"VKey-{item.VirtualKeyId}",
                    Error = ex.Message,
                    Duration = stopwatch.Elapsed
                };
            }
        }
    }
}
using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.Services;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles SpendUpdated events and sends real-time notifications through SignalR.
    /// </summary>
    public class SpendUpdatedHandler : IConsumer<SpendUpdated>
    {
        private readonly ISpendNotificationService _notificationService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ILogger<SpendUpdatedHandler> _logger;

        public SpendUpdatedHandler(
            ISpendNotificationService notificationService,
            IVirtualKeyService virtualKeyService,
            ILogger<SpendUpdatedHandler> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Consume(ConsumeContext<SpendUpdated> context)
        {
            var message = context.Message;

            try
            {
                _logger.LogInformation(
                    "Processing spend notification for Virtual Key {KeyId}: ${Amount:F2} spent, new total: ${NewTotal:F2}",
                    message.KeyId,
                    message.Amount,
                    message.NewTotalSpend);

                // Get virtual key details for budget information
                var virtualKey = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(message.KeyId);
                if (virtualKey == null)
                {
                    _logger.LogWarning("Virtual key {KeyId} not found for spend notification", message.KeyId);
                    return;
                }

                // Extract model and provider from request context if available
                // For now, use defaults - in production, this would come from request metadata
                var model = "unknown";
                var provider = "unknown";
                
                // Get model/provider from message properties if available
                if (context.Headers.TryGetHeader("Model", out var modelHeader))
                {
                    model = modelHeader?.ToString() ?? "unknown";
                }
                if (context.Headers.TryGetHeader("Provider", out var providerHeader))
                {
                    provider = providerHeader?.ToString() ?? "unknown";
                }

                // Send the spend notification
                await _notificationService.NotifySpendUpdateAsync(
                    message.KeyId,
                    message.Amount,
                    message.NewTotalSpend,
                    virtualKey.MaxBudget,
                    model,
                    provider);

                _logger.LogDebug(
                    "Spend notification sent for Virtual Key {KeyId} with correlation ID {CorrelationId}",
                    message.KeyId,
                    message.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error processing spend notification for Virtual Key {KeyId}",
                    message.KeyId);
                
                // Don't throw - we don't want to retry notifications
                // They are best-effort and should not block the main flow
            }
        }
    }
}
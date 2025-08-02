using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Processes spend update requests in ordered fashion per virtual key
    /// Eliminates race conditions and dual update paths
    /// Uses proper dependency injection with IServiceScopeFactory
    /// </summary>
    public class SpendUpdateProcessor : IConsumer<SpendUpdateRequested>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<SpendUpdateProcessor> _logger;

        /// <summary>
        /// Initializes a new instance of the SpendUpdateProcessor
        /// </summary>
        /// <param name="serviceScopeFactory">Service scope factory for creating scoped services</param>
        /// <param name="publishEndpoint">MassTransit publish endpoint for publishing events</param>
        /// <param name="logger">Logger instance</param>
        public SpendUpdateProcessor(
            IServiceScopeFactory serviceScopeFactory,
            IPublishEndpoint publishEndpoint,
            ILogger<SpendUpdateProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes spend update requests in ordered fashion
        /// This replaces the dual update paths (individual + batch) with single ordered processing
        /// </summary>
        /// <param name="context">Message context containing the spend update request</param>
        public async Task Consume(ConsumeContext<SpendUpdateRequested> context)
        {
            var request = context.Message;
            
            if (request.Amount <= 0)
            {
                _logger.LogDebug("Spend update request for key {KeyId} has zero or negative amount {Amount} - skipping", 
                    request.KeyId, request.Amount);
                return;
            }

            // Create a scope to get the repositories
            using var scope = _serviceScopeFactory.CreateScope();
            var virtualKeyRepository = scope.ServiceProvider.GetService<IVirtualKeyRepository>();
            var groupRepository = scope.ServiceProvider.GetService<IVirtualKeyGroupRepository>();
            
            if (virtualKeyRepository == null || groupRepository == null)
            {
                _logger.LogWarning(
                    "Virtual key or group repository not available - cannot process spend update for key {KeyId}. " +
                    "This is expected in Core API context where repositories are not registered.",
                    request.KeyId);
                
                // Still publish the event so other services can react
                // This allows the Admin API or other services to handle the update
                await _publishEndpoint.Publish(new SpendUpdateDeferred
                {
                    KeyId = request.KeyId,
                    Amount = request.Amount,
                    RequestId = request.RequestId,
                    CorrelationId = request.CorrelationId,
                    Reason = "Repository not available in current context"
                });
                
                return;
            }

            try
            {
                _logger.LogDebug("Processing spend update request for key {KeyId}: amount {Amount}, requestId {RequestId}",
                    request.KeyId, request.Amount, request.RequestId);

                // Get current virtual key state
                var virtualKey = await virtualKeyRepository.GetByIdAsync(request.KeyId);
                if (virtualKey == null)
                {
                    _logger.LogWarning("Spend update request for non-existent virtual key {KeyId} - ignoring", request.KeyId);
                    return;
                }

                // Get the key's group
                var group = await groupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
                if (group == null)
                {
                    _logger.LogError("Virtual key {KeyId} has invalid group ID {GroupId}", request.KeyId, virtualKey.VirtualKeyGroupId);
                    return;
                }

                // Calculate new spend total at group level
                var previousSpend = group.LifetimeSpent;
                var newSpend = previousSpend + request.Amount;
                var previousBalance = group.Balance;
                
                // Update the group balance and lifetime spent
                var newBalance = await groupRepository.AdjustBalanceAsync(group.Id, -request.Amount);
                
                var success = newBalance >= 0; // Success if we got a valid balance back
                
                if (success)
                {
                    // Publish SpendUpdated event for cache invalidation and audit
                    await _publishEndpoint.Publish(new SpendUpdated
                    {
                        KeyId = request.KeyId,
                        KeyHash = virtualKey.KeyHash,
                        Amount = request.Amount,
                        NewTotalSpend = newSpend,
                        RequestId = request.RequestId,
                        CorrelationId = request.CorrelationId
                    });

                    _logger.LogInformation(
                        "Spend updated for virtual key {KeyId} in group {GroupId}: {PreviousSpend} + {Amount} = {NewSpend}, new balance: {NewBalance} (requestId: {RequestId})",
                        request.KeyId, group.Id, previousSpend, request.Amount, newSpend, newBalance, request.RequestId);
                    
                    // Check if group balance is depleted
                    if (newBalance <= 0 && previousBalance > 0)
                    {
                        // Balance just hit zero
                        
                        await _publishEndpoint.Publish(new SpendThresholdExceeded
                        {
                            VirtualKeyId = virtualKey.Id,
                            VirtualKeyHash = virtualKey.KeyHash,
                            KeyName = virtualKey.KeyName,
                            CurrentSpend = newSpend,
                            MaxBudget = previousBalance, // The balance that was available
                            AmountOver = -newBalance, // How much we're over
                            BudgetDuration = null, // No longer applicable in bank account model
                            ExceededAt = DateTime.UtcNow,
                            KeyDisabled = false, // We don't auto-disable in this handler
                            CorrelationId = request.CorrelationId
                        });
                        
                        _logger.LogWarning(
                            "Virtual key group {GroupId} for key {KeyId} ({KeyName}) has depleted balance: {NewBalance:C}",
                            group.Id, virtualKey.Id, virtualKey.KeyName, newBalance);
                    }
                }
                else
                {
                    _logger.LogError("Failed to update spend for virtual key group {GroupId} (key {KeyId}) - balance adjustment failed", group.Id, request.KeyId);
                    throw new InvalidOperationException($"Failed to update spend for virtual key group {group.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error processing spend update for virtual key {KeyId}, amount {Amount}, requestId {RequestId}", 
                    request.KeyId, request.Amount, request.RequestId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
        
        // Note: Threshold approaching notifications are no longer applicable in the bank account model
        // Groups have a balance that decreases, not a budget that fills up
    }
}
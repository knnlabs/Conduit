using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Repositories;

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

            // Create a scope to get the repository
            using var scope = _serviceScopeFactory.CreateScope();
            var virtualKeyRepository = scope.ServiceProvider.GetService<IVirtualKeyRepository>();
            
            if (virtualKeyRepository == null)
            {
                _logger.LogWarning(
                    "Virtual key repository not available - cannot process spend update for key {KeyId}. " +
                    "This is expected in Core API context where repository is not registered.",
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

                // Calculate new spend total
                var previousSpend = virtualKey.CurrentSpend;
                var newSpend = previousSpend + request.Amount;
                
                // Update the virtual key spend
                virtualKey.CurrentSpend = newSpend;
                virtualKey.UpdatedAt = DateTime.UtcNow;
                
                var success = await virtualKeyRepository.UpdateAsync(virtualKey);
                
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
                        "Spend updated for virtual key {KeyId}: {PreviousSpend} + {Amount} = {NewSpend} (requestId: {RequestId})",
                        request.KeyId, previousSpend, request.Amount, newSpend, request.RequestId);
                    
                    // Check spend thresholds if budget is configured
                    if (virtualKey.MaxBudget.HasValue && virtualKey.MaxBudget.Value > 0)
                    {
                        var budgetLimit = virtualKey.MaxBudget.Value;
                        var percentageUsed = (newSpend / budgetLimit) * 100;
                        
                        // Check if exceeded
                        if (newSpend > budgetLimit)
                        {
                            await _publishEndpoint.Publish(new SpendThresholdExceeded
                            {
                                VirtualKeyId = virtualKey.Id,
                                VirtualKeyHash = virtualKey.KeyHash,
                                KeyName = virtualKey.KeyName,
                                CurrentSpend = newSpend,
                                MaxBudget = budgetLimit,
                                AmountOver = newSpend - budgetLimit,
                                BudgetDuration = virtualKey.BudgetDuration,
                                ExceededAt = DateTime.UtcNow,
                                KeyDisabled = false, // We don't auto-disable in this handler
                                CorrelationId = request.CorrelationId
                            });
                            
                            _logger.LogWarning(
                                "Virtual key {KeyId} ({KeyName}) has exceeded budget: {CurrentSpend:C} > {MaxBudget:C}",
                                virtualKey.Id, virtualKey.KeyName, newSpend, budgetLimit);
                        }
                        // Check if approaching (80% and 90% thresholds)
                        else if (percentageUsed >= 80 && previousSpend / budgetLimit * 100 < 80)
                        {
                            await PublishThresholdApproaching(virtualKey, newSpend, budgetLimit, 80, request.CorrelationId);
                        }
                        else if (percentageUsed >= 90 && previousSpend / budgetLimit * 100 < 90)
                        {
                            await PublishThresholdApproaching(virtualKey, newSpend, budgetLimit, 90, request.CorrelationId);
                        }
                    }
                }
                else
                {
                    _logger.LogError("Failed to update spend for virtual key {KeyId} - database update returned false", request.KeyId);
                    throw new InvalidOperationException($"Failed to update spend for virtual key {request.KeyId}");
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
        
        /// <summary>
        /// Publishes a spend threshold approaching event
        /// </summary>
        private async Task PublishThresholdApproaching(ConduitLLM.Configuration.Entities.VirtualKey virtualKey, decimal currentSpend, decimal maxBudget, 
            int thresholdPercentage, string correlationId)
        {
            DateTime? budgetResetDate = null;
            if (virtualKey.BudgetStartDate.HasValue && !string.IsNullOrEmpty(virtualKey.BudgetDuration))
            {
                budgetResetDate = virtualKey.BudgetDuration?.ToLower() switch
                {
                    "daily" => virtualKey.BudgetStartDate.Value.AddDays(1),
                    "weekly" => virtualKey.BudgetStartDate.Value.AddDays(7),
                    "monthly" => virtualKey.BudgetStartDate.Value.AddMonths(1),
                    _ => null
                };
            }
            
            await _publishEndpoint.Publish(new SpendThresholdApproaching
            {
                VirtualKeyId = virtualKey.Id,
                VirtualKeyHash = virtualKey.KeyHash,
                KeyName = virtualKey.KeyName,
                CurrentSpend = currentSpend,
                MaxBudget = maxBudget,
                PercentageUsed = (currentSpend / maxBudget) * 100,
                ThresholdPercentage = thresholdPercentage,
                BudgetDuration = virtualKey.BudgetDuration,
                BudgetStartDate = virtualKey.BudgetStartDate,
                BudgetResetDate = budgetResetDate,
                CorrelationId = correlationId
            });
            
            _logger.LogWarning(
                "Virtual key {KeyId} ({KeyName}) is approaching budget threshold: {PercentageUsed:F1}% of {MaxBudget:C} used",
                virtualKey.Id, virtualKey.KeyName, (currentSpend / maxBudget) * 100, maxBudget);
        }
    }
}
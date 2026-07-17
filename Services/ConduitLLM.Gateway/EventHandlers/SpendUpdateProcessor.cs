using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Processes spend update requests in ordered fashion per virtual key
    /// Eliminates race conditions and dual update paths
    /// Uses proper dependency injection with IServiceScopeFactory
    /// </summary>
    public class SpendUpdateProcessor : IEventHandler<SpendUpdateRequested>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IEventBus _eventBus;
        private readonly ILogger<SpendUpdateProcessor> _logger;

        /// <summary>
        /// Initializes a new instance of the SpendUpdateProcessor
        /// </summary>
        /// <param name="serviceScopeFactory">Service scope factory for creating scoped services</param>
        /// <param name="eventBus">Event bus for publishing follow-on events</param>
        /// <param name="logger">Logger instance</param>
        public SpendUpdateProcessor(
            IServiceScopeFactory serviceScopeFactory,
            IEventBus eventBus,
            ILogger<SpendUpdateProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes spend update requests in ordered fashion
        /// This replaces the dual update paths (individual + batch) with single ordered processing
        /// </summary>
        /// <param name="request">The spend update request</param>
        /// <param name="context">Delivery context</param>
        public async Task HandleAsync(SpendUpdateRequested request, IEventContext context)
        {
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
                    "This is expected in Gateway API context where repositories are not registered.",
                    request.KeyId);
                
                // Still publish the event so other services can react
                // This allows the Admin API or other services to handle the update
                await _eventBus.PublishAsync(new SpendUpdateDeferred
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

                var previousBalance = group.Balance;
                var description = $"API usage by virtual key #{request.KeyId}";

                // Debit the group exactly once per RequestId (#927): the idempotency key
                // rides the ledger row inside the same atomic save as the balance change,
                // so an at-least-once redelivery cannot double-charge.
                BalanceAdjustmentResult result;
                if (!string.IsNullOrWhiteSpace(request.RequestId))
                {
                    result = await groupRepository.AdjustBalanceIdempotentAsync(
                        group.Id,
                        -request.Amount,
                        SpendIdempotency.KeyFor(request.RequestId),
                        description,
                        "System",
                        ReferenceType.VirtualKey,
                        request.KeyId.ToString());
                }
                else
                {
                    var balance = await groupRepository.AdjustBalanceAsync(
                        group.Id,
                        -request.Amount,
                        description,
                        "System",
                        ReferenceType.VirtualKey,
                        request.KeyId.ToString());
                    result = new BalanceAdjustmentResult(balance, group.LifetimeSpent + request.Amount, Applied: true);
                }

                var newBalance = result.NewBalance;
                var newSpend = result.LifetimeSpent;

                if (!result.Applied)
                {
                    _logger.LogWarning(
                        "Spend update for key {KeyId} with requestId {RequestId} was already applied - republishing notification only",
                        request.KeyId, request.RequestId);
                }

                // Publish SpendUpdated for cache invalidation and audit. Also republished on
                // a duplicate delivery, in case the first attempt crashed after the debit
                // committed but before this notification went out.
                await _eventBus.PublishAsync(new SpendUpdated
                {
                    KeyId = request.KeyId,
                    KeyHash = virtualKey.KeyHash,
                    Amount = request.Amount,
                    NewTotalSpend = newSpend,
                    RequestId = request.RequestId,
                    CorrelationId = request.CorrelationId
                });

                _logger.LogInformation(
                    "Spend updated for virtual key {KeyId} in group {GroupId}: +{Amount} = {NewSpend}, new balance: {NewBalance} (requestId: {RequestId}, applied: {Applied})",
                    request.KeyId, group.Id, request.Amount, newSpend, newBalance, request.RequestId, result.Applied);

                // Check if this debit depleted the group balance (skipped on duplicate:
                // the crossing was already reported when the debit first applied)
                if (result.Applied && newBalance <= 0 && previousBalance > 0)
                {
                    await _eventBus.PublishAsync(new SpendThresholdExceeded
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
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error processing spend update for virtual key {KeyId}, amount {Amount}, requestId {RequestId}", 
                    request.KeyId, request.Amount, request.RequestId);
                throw; // Re-throw to trigger the endpoint retry policy
            }
        }
        
        // Note: Threshold approaching notifications are no longer applicable in the bank account model
        // Groups have a balance that decreases, not a budget that fills up
    }
}
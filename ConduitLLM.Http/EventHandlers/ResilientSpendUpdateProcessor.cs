using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Resilient implementation of spend update processing with fallback mechanisms
    /// </summary>
    public class ResilientSpendUpdateProcessor : ResilientEventHandlerBase<SpendUpdateRequested>
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeyGroupRepository _groupRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly IDistributedCache _cache;
        private readonly IDistributedLockService _lockService;

        public ResilientSpendUpdateProcessor(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeyGroupRepository groupRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            IDistributedCache cache,
            IDistributedLockService lockService,
            ILogger<ResilientSpendUpdateProcessor> logger)
            : base(logger)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        }

        protected override async Task HandleEventAsync(SpendUpdateRequested message, CancellationToken cancellationToken)
        {
            // Get the virtual key first to find its group
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(message.KeyId);
            if (virtualKey == null)
            {
                Logger.LogError("Virtual key {KeyId} not found in database", message.KeyId);
                throw new InvalidOperationException($"Virtual key {message.KeyId} not found");
            }

            // Get the key's group
            var group = await _groupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
            if (group == null)
            {
                Logger.LogError("Virtual key {KeyId} has invalid group ID {GroupId}", message.KeyId, virtualKey.VirtualKeyGroupId);
                throw new InvalidOperationException($"Virtual key {message.KeyId} has invalid group");
            }

            // Use group-based locking to handle concurrent updates properly
            var lockKey = $"group_spend_update_{group.Id}";
            
            // Acquire distributed lock to prevent concurrent updates
            using var lockHandle = await _lockService.AcquireLockAsync(
                lockKey,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (lockHandle == null)
            {
                throw new InvalidOperationException(
                    $"Failed to acquire lock for virtual key group {group.Id}. Another update may be in progress.");
            }

            // Check if group has sufficient balance
            if (group.Balance < message.Amount)
            {
                Logger.LogWarning(
                    "Virtual key group {GroupId} has insufficient balance. Current: ${Current:F2}, Requested: ${Amount:F2}",
                    group.Id, group.Balance, message.Amount);
                
                // This is a business rule violation, not a technical error
                return;
            }

            // Update the group balance and lifetime spent
            var newBalance = await _groupRepository.AdjustBalanceAsync(group.Id, -message.Amount);
            
            // Update virtual key's updated timestamp
            virtualKey.UpdatedAt = DateTime.UtcNow;
            await _virtualKeyRepository.UpdateAsync(virtualKey);

            // Record in spend history
            var spendHistory = new Configuration.Entities.VirtualKeySpendHistory
            {
                VirtualKeyId = message.KeyId,
                Amount = message.Amount,
                Date = DateTime.UtcNow.Date,
                Timestamp = DateTime.UtcNow
            };
            
            // Use the repository's Create method instead of AddAsync
            await _spendHistoryRepository.CreateAsync(spendHistory);

            // Invalidate cache for both key and group
            var keyCacheKey = $"vkey_spend:{message.KeyId}";
            var groupCacheKey = $"vkey_group:{group.Id}";
            await _cache.RemoveAsync(keyCacheKey, cancellationToken);
            await _cache.RemoveAsync(groupCacheKey, cancellationToken);

            Logger.LogInformation(
                "Successfully updated spend for virtual key {KeyId} in group {GroupId}: -${Amount:F2}, New Balance: ${Balance:F2}",
                message.KeyId, group.Id, message.Amount, newBalance);
        }

        protected override async Task HandleEventFallbackAsync(SpendUpdateRequested message, CancellationToken cancellationToken)
        {
            Logger.LogWarning(
                "Executing fallback for spend update. Virtual Key: {KeyId}, Amount: ${Amount:F2}",
                message.KeyId, message.Amount);

            try
            {
                // Fallback 1: Try to store in cache for later processing
                var pendingKey = $"pending_spend:{message.KeyId}:{Guid.NewGuid()}";
                var serialized = System.Text.Json.JsonSerializer.Serialize(message);
                
                await _cache.SetStringAsync(
                    pendingKey,
                    serialized,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    },
                    cancellationToken);

                Logger.LogInformation(
                    "Spend update cached for later processing. Key: {PendingKey}",
                    pendingKey);

                // TODO: A background service should process these pending updates
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Failed to cache spend update for fallback processing");

                // Fallback 2: Log to a file or external system
                // This ensures we never lose spend data
                await LogSpendUpdateToFileAsync(message);
            }
        }

        private async Task LogSpendUpdateToFileAsync(SpendUpdateRequested message)
        {
            var logEntry = $"{DateTime.UtcNow:O}|{message.KeyId}|{message.Amount}|N/A|N/A|{message.RequestId}";
            var logFile = $"spend_updates_fallback_{DateTime.UtcNow:yyyyMMdd}.log";
            
            try
            {
                await System.IO.File.AppendAllTextAsync(logFile, logEntry + Environment.NewLine);
                Logger.LogWarning(
                    "Spend update logged to fallback file: {LogFile}",
                    logFile);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex,
                    "Failed to log spend update to fallback file. Data may be lost: {Message}",
                    logEntry);
            }
        }

        protected override bool IsTransientException(Exception ex)
        {
            // Add database-specific transient error detection
            if (ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return base.IsTransientException(ex);
        }

        protected override int GetCircuitBreakerThreshold() => 10; // Higher threshold for spend updates
        protected override TimeSpan GetCircuitBreakerDuration() => TimeSpan.FromMinutes(5);
        protected override TimeSpan GetTimeout() => TimeSpan.FromSeconds(15); // Faster timeout for spend updates
    }
}
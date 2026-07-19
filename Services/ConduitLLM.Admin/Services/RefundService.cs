using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for processing refunds for virtual key group usage
/// </summary>
public class RefundService : IRefundService
{
    private readonly ICostCalculationService _costCalculationService;
    private readonly IVirtualKeyGroupRepository _groupRepository;
    private readonly IConfigurationDbContext _context;
    private readonly IRequestLogRepository? _requestLogRepository;
    private readonly ILogger<RefundService> _logger;

    /// <summary>
    /// Initializes a new instance of the RefundService
    /// </summary>
    public RefundService(
        ICostCalculationService costCalculationService,
        IVirtualKeyGroupRepository groupRepository,
        IConfigurationDbContext context,
        ILogger<RefundService> logger,
        IRequestLogRepository? requestLogRepository = null)
    {
        _costCalculationService = costCalculationService;
        _groupRepository = groupRepository;
        _context = context;
        _logger = logger;
        _requestLogRepository = requestLogRepository;
    }

    /// <inheritdoc />
    public async Task<RefundResult> ProcessRefundAsync(
        int virtualKeyGroupId,
        string modelId,
        Usage originalUsage,
        Usage refundUsage,
        string refundReason,
        string originalTransactionId,
        string initiatedBy,
        string? initiatedByUserId,
        int? requestLogId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing refund for group {GroupId}, model {ModelId}, initiated by {InitiatedBy}",
            virtualKeyGroupId, modelId, initiatedBy);

        if (!long.TryParse(originalTransactionId?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture,
                out var originalTransactionKey) || originalTransactionKey <= 0)
        {
            throw new ArgumentException(
                "A valid original debit transaction ID is required.", nameof(originalTransactionId));
        }

        // Store and compare the canonical representation so alternate numeric formatting cannot
        // bypass cumulative-refund checks.
        originalTransactionId = originalTransactionKey.ToString(CultureInfo.InvariantCulture);

        // Validate group exists
        var group = await _groupRepository.GetByIdAsync(virtualKeyGroupId);
        if (group == null)
        {
            _logger.LogWarning("Refund rejected: virtual key group {GroupId} not found", virtualKeyGroupId);
            throw new InvalidOperationException($"Virtual key group {virtualKeyGroupId} not found");
        }

        var originalTransaction = await _context.VirtualKeyGroupTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == originalTransactionKey, cancellationToken);

        if (originalTransaction == null)
        {
            throw new ArgumentException(
                $"Original transaction {originalTransactionId} was not found.", nameof(originalTransactionId));
        }

        if (originalTransaction.VirtualKeyGroupId != virtualKeyGroupId ||
            originalTransaction.TransactionType != TransactionType.Debit)
        {
            throw new ArgumentException(
                $"Transaction {originalTransactionId} is not a debit for virtual key group {virtualKeyGroupId}.",
                nameof(originalTransactionId));
        }

        // Always calculate against the durable debit amount. Model prices and even the pricing model
        // may have changed since the request, while this ledger value is the amount actually charged.
        var originalChargeContext = new ProviderCostRefundContext
        {
            OriginalChargedCost = originalTransaction.Amount
        };

        // Calculate refund using the cost calculation service
        var refundResult = await _costCalculationService.CalculateRefundAsync(
            modelId,
            originalUsage,
            refundUsage,
            refundReason,
            originalTransactionId,
            originalChargeContext,
            cancellationToken);

        // Validation failures must never be treated as warnings. Reject before mutating the balance
        // even if a calculator bug returns a positive amount alongside validation messages.
        if (refundResult.ValidationMessages.Count > 0)
        {
            var errorMessage = string.Join("; ", refundResult.ValidationMessages);
            _logger.LogWarning(
                "Refund validation failed for group {GroupId}, model {ModelId}: {ValidationErrors}",
                virtualKeyGroupId, modelId, errorMessage);
            throw new ArgumentException($"Refund validation failed: {errorMessage}");
        }

        // Do not trust calculator implementations to echo this field correctly. It is the durable
        // link from the refund result and ledger entry back to the charge.
        refundResult.OriginalTransactionId = originalTransactionId;

        var idempotencyKey = CreateIdempotencyKey(
            virtualKeyGroupId, originalTransactionId, modelId, refundUsage);

        await ExecuteRefundTransactionAsync(async ct =>
        {
            // Re-read all mutable state inside the serializable transaction. This makes the aggregate
            // check safe when two admins refund the same charge concurrently.
            var trackedGroup = await _context.VirtualKeyGroups
                .SingleOrDefaultAsync(g => g.Id == virtualKeyGroupId, ct)
                ?? throw new InvalidOperationException($"Virtual key group {virtualKeyGroupId} not found");

            var chargeAmount = await _context.VirtualKeyGroupTransactions
                .IgnoreQueryFilters()
                .Where(t => t.Id == originalTransactionKey
                    && t.VirtualKeyGroupId == virtualKeyGroupId
                    && t.TransactionType == TransactionType.Debit)
                .Select(t => (decimal?)t.Amount)
                .SingleOrDefaultAsync(ct)
                ?? throw new ArgumentException(
                    $"Transaction {originalTransactionId} is not a debit for virtual key group {virtualKeyGroupId}.",
                    nameof(originalTransactionId));

            var duplicateRequest = await _context.VirtualKeyGroupTransactions
                .IgnoreQueryFilters()
                .AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct);
            if (duplicateRequest)
            {
                throw new InvalidOperationException(
                    $"This refund request for transaction {originalTransactionId} has already been processed.");
            }

            var refundedAmount = await _context.VirtualKeyGroupTransactions
                .IgnoreQueryFilters()
                .Where(t => t.VirtualKeyGroupId == virtualKeyGroupId
                    && t.TransactionType == TransactionType.Refund
                    && t.ReferenceId == originalTransactionId)
                .SumAsync(t => t.Amount, ct);

            if (refundResult.RefundAmount <= 0)
            {
                throw new ArgumentException("Refund amount must be greater than zero.");
            }

            if (refundedAmount + refundResult.RefundAmount > chargeAmount)
            {
                throw new InvalidOperationException(
                    $"Refund would exceed original transaction {originalTransactionId}: " +
                    $"charged {chargeAmount}, already refunded {refundedAmount}, requested {refundResult.RefundAmount}.");
            }

            // Update group balance
            var previousBalance = trackedGroup.Balance;
            trackedGroup.Balance += refundResult.RefundAmount; // Refunds are always positive, so we add
            trackedGroup.UpdatedAt = DateTime.UtcNow;

            // Create transaction record with Refund type
            var transaction = new VirtualKeyGroupTransaction
            {
                VirtualKeyGroupId = virtualKeyGroupId,
                TransactionType = TransactionType.Refund,
                Amount = refundResult.RefundAmount, // Always positive
                BalanceAfter = trackedGroup.Balance,
                ReferenceType = ReferenceType.Manual, // Refunds are manual administrative actions
                ReferenceId = originalTransactionId,
                IdempotencyKey = idempotencyKey,
                Description = $"Refund: {refundReason} (Model: {modelId})",
                InitiatedBy = initiatedBy,
                InitiatedByUserId = initiatedByUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.VirtualKeyGroupTransactions.Add(transaction);
            await _context.SaveChangesAsync(ct);

            refundResult.RefundTransactionId = transaction.Id;

            _logger.LogInformation(
                "Processed refund for group {GroupId}: {RefundAmount:C}. Model: {ModelId}, Reason: {RefundReason}, Previous Balance: {PreviousBalance:C}, New Balance: {NewBalance:C}, Transaction ID: {TransactionId}",
                virtualKeyGroupId,
                refundResult.RefundAmount,
                modelId,
                refundReason,
                previousBalance,
                trackedGroup.Balance,
                transaction.Id);
        }, cancellationToken);

        return refundResult;
    }

    private async Task ExecuteRefundTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        if (_context is not DbContext dbContext || !dbContext.Database.IsRelational())
        {
            await work(cancellationToken);
            return;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            await work(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }

    private static string CreateIdempotencyKey(
        int groupId,
        string originalTransactionId,
        string modelId,
        Usage refundUsage)
    {
        var request = string.Join('|',
            groupId.ToString(CultureInfo.InvariantCulture),
            originalTransactionId,
            modelId,
            refundUsage.PromptTokens,
            refundUsage.CompletionTokens,
            refundUsage.TotalTokens,
            refundUsage.CachedInputTokens,
            refundUsage.CachedWriteTokens,
            refundUsage.ReasoningTokens,
            refundUsage.ImageCount,
            refundUsage.ImageQuality,
            refundUsage.ImageResolution,
            refundUsage.VideoDurationSeconds?.ToString("R", CultureInfo.InvariantCulture),
            refundUsage.VideoResolution,
            refundUsage.SearchUnits,
            refundUsage.InferenceSteps,
            refundUsage.IsBatch);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request)));
        return $"refund:{groupId}:{originalTransactionId}:{hash[..32]}";
    }
}

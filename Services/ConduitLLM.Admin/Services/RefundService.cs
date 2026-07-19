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
        string? originalTransactionId,
        string initiatedBy,
        string? initiatedByUserId,
        int? requestLogId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing refund for group {GroupId}, model {ModelId}, initiated by {InitiatedBy}",
            virtualKeyGroupId, modelId, initiatedBy);

        // Validate group exists
        var group = await _groupRepository.GetByIdAsync(virtualKeyGroupId);
        if (group == null)
        {
            _logger.LogWarning("Refund rejected: virtual key group {GroupId} not found", virtualKeyGroupId);
            throw new InvalidOperationException($"Virtual key group {virtualKeyGroupId} not found");
        }

        // Idempotency: when an original transaction is referenced, refuse to refund it more than once.
        // This prevents client retries / double-clicks from crediting the balance repeatedly. The
        // reference is only enforceable when the caller supplies originalTransactionId; without it we
        // cannot deduplicate, so we warn.
        // NOTE: this pre-check is not atomic against two simultaneous refunds of the same transaction;
        // a filtered unique index on (VirtualKeyGroupId, ReferenceId) WHERE TransactionType = Refund is
        // the durable guard and is tracked as a follow-up (issue #990).
        if (!string.IsNullOrEmpty(originalTransactionId))
        {
            var alreadyRefunded = await _context.VirtualKeyGroupTransactions
                .AnyAsync(
                    t => t.VirtualKeyGroupId == virtualKeyGroupId
                        && t.TransactionType == TransactionType.Refund
                        && t.ReferenceId == originalTransactionId,
                    cancellationToken);

            if (alreadyRefunded)
            {
                _logger.LogWarning(
                    "Refund rejected: transaction {OriginalTransactionId} for group {GroupId} has already been refunded",
                    originalTransactionId, virtualKeyGroupId);
                throw new InvalidOperationException(
                    $"Transaction {originalTransactionId} has already been refunded for group {virtualKeyGroupId}");
            }
        }
        else
        {
            _logger.LogWarning(
                "Refund for group {GroupId} was submitted without an original transaction id; " +
                "duplicate-refund protection cannot be enforced for this request",
                virtualKeyGroupId);
        }

        // If the original request was billed from a trusted provider-reported cost, it has no per-unit
        // rates to recompute a refund from. Look up the original request log and, when it was billed
        // that way, prorate the refund from the amount actually charged.
        ProviderCostRefundContext? providerCostContext = null;
        if (requestLogId.HasValue && _requestLogRepository != null)
        {
            var requestLog = await _requestLogRepository.GetByIdAsync(requestLogId.Value, cancellationToken);
            if (requestLog?.BillingMethod == RequestBillingMethod.ProviderReportedCost)
            {
                providerCostContext = new ProviderCostRefundContext { OriginalChargedCost = requestLog.Cost };
                _logger.LogInformation(
                    "Refund for group {GroupId} references provider-cost-billed request log {RequestLogId} " +
                    "(charged {Charged}); refund will be prorated from the charged amount.",
                    virtualKeyGroupId, requestLogId.Value, requestLog.Cost);
            }
        }

        // Calculate refund using the cost calculation service
        var refundResult = await _costCalculationService.CalculateRefundAsync(
            modelId,
            originalUsage,
            refundUsage,
            refundReason,
            originalTransactionId,
            providerCostContext,
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

        // Update group balance
        var previousBalance = group.Balance;
        group.Balance += refundResult.RefundAmount; // Refunds are always positive, so we add
        group.UpdatedAt = DateTime.UtcNow;

        // Create transaction record with Refund type
        var transaction = new VirtualKeyGroupTransaction
        {
            VirtualKeyGroupId = virtualKeyGroupId,
            TransactionType = TransactionType.Refund,
            Amount = refundResult.RefundAmount, // Always positive
            BalanceAfter = group.Balance,
            ReferenceType = ReferenceType.Manual, // Refunds are manual administrative actions
            ReferenceId = originalTransactionId,
            Description = $"Refund: {refundReason} (Model: {modelId})",
            InitiatedBy = initiatedBy,
            InitiatedByUserId = initiatedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        // Attach and update the group entity (it was fetched with AsNoTracking)
        _context.VirtualKeyGroups.Update(group);
        _context.VirtualKeyGroupTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        // Record the id of the refund transaction we just created. We deliberately do NOT overwrite
        // OriginalTransactionId here — that field preserves the caller-supplied linkage to the charge
        // being refunded, which is what makes the idempotency check above possible.
        // Note: The transaction ID is generated by the database after SaveChangesAsync.
        refundResult.RefundTransactionId = transaction.Id;

        _logger.LogInformation(
            "Processed refund for group {GroupId}: {RefundAmount:C}. Model: {ModelId}, Reason: {RefundReason}, Previous Balance: {PreviousBalance:C}, New Balance: {NewBalance:C}, Transaction ID: {TransactionId}",
            virtualKeyGroupId,
            refundResult.RefundAmount,
            modelId,
            refundReason,
            previousBalance,
            group.Balance,
            transaction.Id);

        return refundResult;
    }
}

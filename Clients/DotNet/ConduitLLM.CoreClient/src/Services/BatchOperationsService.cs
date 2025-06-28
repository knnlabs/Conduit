using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Exceptions;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for performing batch operations on the Conduit API
/// </summary>
public class BatchOperationsService
{
    private readonly BaseClient _client;

    /// <summary>
    /// Initializes a new instance of the BatchOperationsService
    /// </summary>
    /// <param name="client">The base client for API communication</param>
    public BatchOperationsService(BaseClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Performs a batch spend update operation
    /// </summary>
    /// <param name="request">The batch spend update request containing up to 10,000 spend updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The batch operation start response</returns>
    /// <exception cref="ArgumentException">Thrown when the request contains more than 10,000 items</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API returns an error</exception>
    public async Task<BatchOperationStartResponse> BatchSpendUpdateAsync(
        BatchSpendUpdateRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (request.SpendUpdates.Count > 10000)
        {
            throw new ArgumentException("Batch spend updates cannot exceed 10,000 items");
        }

        return await _client.PostForServiceAsync<BatchOperationStartResponse>(
            "/v1/batch/spend-updates", 
            request, 
            cancellationToken);
    }

    /// <summary>
    /// Performs a batch virtual key update operation (requires admin permissions)
    /// </summary>
    /// <param name="request">The batch virtual key update request containing up to 1,000 virtual key updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The batch operation start response</returns>
    /// <exception cref="ArgumentException">Thrown when the request contains more than 1,000 items</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API returns an error</exception>
    public async Task<BatchOperationStartResponse> BatchVirtualKeyUpdateAsync(
        BatchVirtualKeyUpdateRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (request.VirtualKeyUpdates.Count > 1000)
        {
            throw new ArgumentException("Batch virtual key updates cannot exceed 1,000 items");
        }

        return await _client.PostForServiceAsync<BatchOperationStartResponse>(
            "/v1/batch/virtual-key-updates", 
            request, 
            cancellationToken);
    }

    /// <summary>
    /// Performs a batch webhook send operation
    /// </summary>
    /// <param name="request">The batch webhook send request containing up to 5,000 webhook sends</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The batch operation start response</returns>
    /// <exception cref="ArgumentException">Thrown when the request contains more than 5,000 items</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API returns an error</exception>
    public async Task<BatchOperationStartResponse> BatchWebhookSendAsync(
        BatchWebhookSendRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (request.WebhookSends.Count > 5000)
        {
            throw new ArgumentException("Batch webhook sends cannot exceed 5,000 items");
        }

        return await _client.PostForServiceAsync<BatchOperationStartResponse>(
            "/v1/batch/webhook-sends", 
            request, 
            cancellationToken);
    }

    /// <summary>
    /// Gets the status of a batch operation
    /// </summary>
    /// <param name="operationId">The unique identifier of the batch operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The batch operation status response</returns>
    /// <exception cref="ConduitCoreException">Thrown when the API returns an error</exception>
    public async Task<BatchOperationStatusResponse> GetOperationStatusAsync(
        string operationId, 
        CancellationToken cancellationToken = default)
    {
        return await _client.GetForServiceAsync<BatchOperationStatusResponse>(
            $"/v1/batch/operations/{operationId}", 
            cancellationToken);
    }

    /// <summary>
    /// Cancels a running batch operation
    /// </summary>
    /// <param name="operationId">The unique identifier of the batch operation to cancel</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated batch operation status response</returns>
    /// <exception cref="ConduitCoreException">Thrown when the API returns an error</exception>
    public async Task<BatchOperationStatusResponse> CancelOperationAsync(
        string operationId, 
        CancellationToken cancellationToken = default)
    {
        return await _client.PostForServiceAsync<BatchOperationStatusResponse>(
            $"/v1/batch/operations/{operationId}/cancel", 
            new { }, 
            cancellationToken);
    }

    /// <summary>
    /// Polls a batch operation until completion or timeout
    /// </summary>
    /// <param name="operationId">The unique identifier of the batch operation</param>
    /// <param name="pollingInterval">How often to check the status (default: 5 seconds)</param>
    /// <param name="timeout">Maximum time to wait for completion (default: 10 minutes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The final batch operation status response</returns>
    /// <exception cref="TimeoutException">Thrown when the operation doesn't complete within the timeout</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API returns an error</exception>
    public async Task<BatchOperationStatusResponse> PollOperationAsync(
        string operationId,
        TimeSpan? pollingInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        pollingInterval ??= TimeSpan.FromSeconds(5);
        timeout ??= TimeSpan.FromMinutes(10);

        using var timeoutCts = new CancellationTokenSource(timeout.Value);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        BatchOperationStatusResponse? lastStatus = null;

        try
        {
            while (!combinedCts.Token.IsCancellationRequested)
            {
                lastStatus = await GetOperationStatusAsync(operationId, combinedCts.Token);

                if (lastStatus.Status == BatchOperationStatusEnum.Completed ||
                    lastStatus.Status == BatchOperationStatusEnum.Failed ||
                    lastStatus.Status == BatchOperationStatusEnum.Cancelled ||
                    lastStatus.Status == BatchOperationStatusEnum.PartiallyCompleted)
                {
                    return lastStatus;
                }

                await Task.Delay(pollingInterval.Value, combinedCts.Token);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Batch operation {operationId} did not complete within {timeout.Value}. " +
                $"Last status: {lastStatus?.Status}");
        }

        throw new OperationCanceledException("Batch operation polling was cancelled");
    }

    /// <summary>
    /// Creates a batch spend update request with validation
    /// </summary>
    /// <param name="spendUpdates">The list of spend updates (max 10,000)</param>
    /// <returns>A validated batch spend update request</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static BatchSpendUpdateRequest CreateSpendUpdateRequest(IEnumerable<SpendUpdateDto> spendUpdates)
    {
        var spendUpdateList = spendUpdates.ToList();
        
        if (spendUpdateList.Count > 10000)
        {
            throw new ArgumentException("Cannot create batch with more than 10,000 spend updates");
        }

        if (spendUpdateList.Count == 0)
        {
            throw new ArgumentException("Cannot create empty batch spend update request");
        }

        // Validate each item
        foreach (var (update, index) in spendUpdateList.Select((u, i) => (u, i)))
        {
            if (update.VirtualKeyId <= 0)
            {
                throw new ArgumentException($"Invalid VirtualKeyId at index {index}: {update.VirtualKeyId}");
            }

            if (update.Amount <= 0 || update.Amount > 1000000)
            {
                throw new ArgumentException($"Invalid Amount at index {index}: {update.Amount}. Must be between 0.0001 and 1,000,000");
            }

            if (string.IsNullOrWhiteSpace(update.Model))
            {
                throw new ArgumentException($"Model cannot be empty at index {index}");
            }

            if (string.IsNullOrWhiteSpace(update.Provider))
            {
                throw new ArgumentException($"Provider cannot be empty at index {index}");
            }
        }

        return new BatchSpendUpdateRequest { SpendUpdates = spendUpdateList };
    }

    /// <summary>
    /// Creates a batch virtual key update request with validation
    /// </summary>
    /// <param name="virtualKeyUpdates">The list of virtual key updates (max 1,000)</param>
    /// <returns>A validated batch virtual key update request</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static BatchVirtualKeyUpdateRequest CreateVirtualKeyUpdateRequest(IEnumerable<VirtualKeyUpdateDto> virtualKeyUpdates)
    {
        var updateList = virtualKeyUpdates.ToList();
        
        if (updateList.Count > 1000)
        {
            throw new ArgumentException("Cannot create batch with more than 1,000 virtual key updates");
        }

        if (updateList.Count == 0)
        {
            throw new ArgumentException("Cannot create empty batch virtual key update request");
        }

        // Validate each item
        foreach (var (update, index) in updateList.Select((u, i) => (u, i)))
        {
            if (update.VirtualKeyId <= 0)
            {
                throw new ArgumentException($"Invalid VirtualKeyId at index {index}: {update.VirtualKeyId}");
            }

            if (update.MaxBudget.HasValue && update.MaxBudget.Value < 0)
            {
                throw new ArgumentException($"Invalid MaxBudget at index {index}: {update.MaxBudget}. Cannot be negative");
            }

            if (update.ExpiresAt.HasValue && update.ExpiresAt.Value < DateTime.UtcNow)
            {
                throw new ArgumentException($"Invalid ExpiresAt at index {index}: {update.ExpiresAt}. Cannot be in the past");
            }
        }

        return new BatchVirtualKeyUpdateRequest { VirtualKeyUpdates = updateList };
    }

    /// <summary>
    /// Creates a batch webhook send request with validation
    /// </summary>
    /// <param name="webhookSends">The list of webhook sends (max 5,000)</param>
    /// <returns>A validated batch webhook send request</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static BatchWebhookSendRequest CreateWebhookSendRequest(IEnumerable<WebhookSendDto> webhookSends)
    {
        var webhookSendList = webhookSends.ToList();
        
        if (webhookSendList.Count > 5000)
        {
            throw new ArgumentException("Cannot create batch with more than 5,000 webhook sends");
        }

        if (webhookSendList.Count == 0)
        {
            throw new ArgumentException("Cannot create empty batch webhook send request");
        }

        // Validate each item
        foreach (var (webhook, index) in webhookSendList.Select((w, i) => (w, i)))
        {
            if (string.IsNullOrWhiteSpace(webhook.Url))
            {
                throw new ArgumentException($"URL cannot be empty at index {index}");
            }

            if (!Uri.TryCreate(webhook.Url, UriKind.Absolute, out var uri) || 
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new ArgumentException($"Invalid URL at index {index}: {webhook.Url}");
            }

            if (string.IsNullOrWhiteSpace(webhook.EventType))
            {
                throw new ArgumentException($"EventType cannot be empty at index {index}");
            }

            if (webhook.Payload.Count == 0)
            {
                throw new ArgumentException($"Payload cannot be empty at index {index}");
            }
        }

        return new BatchWebhookSendRequest { WebhookSends = webhookSendList };
    }
}
import { BaseClient } from '../client/BaseClient';
import {
  BatchSpendUpdateRequest,
  BatchVirtualKeyUpdateRequest,
  BatchWebhookSendRequest,
  BatchOperationStartResponse,
  BatchOperationStatusResponse,
  BatchOperationStatusEnum,
  SpendUpdateDto,
  VirtualKeyUpdateDto,
  WebhookSendDto,
  BatchOperationPollOptions,
  BatchValidationOptions,
  BatchValidationResult
} from '../models/batchOperations';

/**
 * Service for performing batch operations on the Conduit Core API
 */
export class BatchOperationsService {
  constructor(private client: BaseClient) {}

  /**
   * Performs a batch spend update operation
   * 
   * @param request - The batch spend update request containing up to 10,000 spend updates
   * @returns Promise<BatchOperationStartResponse> The batch operation start response
   * @throws {ConduitCoreError} When the API request fails or request validation fails
   * 
   * @example
   * ```typescript
   * const spendUpdates = [
   *   { virtualKeyId: 1, amount: 10.50, model: 'gpt-4', provider: 'openai' },
   *   { virtualKeyId: 2, amount: 5.25, model: 'claude-3', provider: 'anthropic' }
   * ];
   * 
   * const startResponse = await coreClient.batchOperations.batchSpendUpdate({
   *   spendUpdates
   * });
   * 
   * console.log(`Started batch operation: ${startResponse.operationId}`);
   * console.log(`Track progress with task ID: ${startResponse.taskId}`);
   * ```
   */
  async batchSpendUpdate(request: BatchSpendUpdateRequest): Promise<BatchOperationStartResponse> {
    // Validate request
    if (request.spendUpdates.length > 10000) {
      throw new Error('Batch spend updates cannot exceed 10,000 items');
    }

    if (request.spendUpdates.length === 0) {
      throw new Error('Cannot create empty batch spend update request');
    }

    return this.client['request']<BatchOperationStartResponse>({
      method: 'POST',
      url: '/v1/batch/spend-updates',
      data: request,
    });
  }

  /**
   * Performs a batch virtual key update operation (requires admin permissions)
   * 
   * @param request - The batch virtual key update request containing up to 1,000 virtual key updates
   * @returns Promise<BatchOperationStartResponse> The batch operation start response
   * @throws {ConduitCoreError} When the API request fails or request validation fails
   * 
   * @example
   * ```typescript
   * const virtualKeyUpdates = [
   *   { virtualKeyId: 1, maxBudget: 1000, isEnabled: true },
   *   { virtualKeyId: 2, allowedModels: ['gpt-4', 'gpt-3.5-turbo'] }
   * ];
   * 
   * const startResponse = await coreClient.batchOperations.batchVirtualKeyUpdate({
   *   virtualKeyUpdates
   * });
   * 
   * console.log(`Started virtual key batch operation: ${startResponse.operationId}`);
   * ```
   */
  async batchVirtualKeyUpdate(request: BatchVirtualKeyUpdateRequest): Promise<BatchOperationStartResponse> {
    // Validate request
    if (request.virtualKeyUpdates.length > 1000) {
      throw new Error('Batch virtual key updates cannot exceed 1,000 items');
    }

    if (request.virtualKeyUpdates.length === 0) {
      throw new Error('Cannot create empty batch virtual key update request');
    }

    return this.client['request']<BatchOperationStartResponse>({
      method: 'POST',
      url: '/v1/batch/virtual-key-updates',
      data: request,
    });
  }

  /**
   * Performs a batch webhook send operation
   * 
   * @param request - The batch webhook send request containing up to 5,000 webhook sends
   * @returns Promise<BatchOperationStartResponse> The batch operation start response
   * @throws {ConduitCoreError} When the API request fails or request validation fails
   * 
   * @example
   * ```typescript
   * const webhookSends = [
   *   {
   *     url: 'https://example.com/webhook',
   *     eventType: 'spend_update',
   *     payload: { userId: 123, amount: 10.50 },
   *     headers: { 'X-Custom-Header': 'value' }
   *   }
   * ];
   * 
   * const startResponse = await coreClient.batchOperations.batchWebhookSend({
   *   webhookSends
   * });
   * 
   * console.log(`Started webhook batch operation: ${startResponse.operationId}`);
   * ```
   */
  async batchWebhookSend(request: BatchWebhookSendRequest): Promise<BatchOperationStartResponse> {
    // Validate request
    if (request.webhookSends.length > 5000) {
      throw new Error('Batch webhook sends cannot exceed 5,000 items');
    }

    if (request.webhookSends.length === 0) {
      throw new Error('Cannot create empty batch webhook send request');
    }

    return this.client['request']<BatchOperationStartResponse>({
      method: 'POST',
      url: '/v1/batch/webhook-sends',
      data: request,
    });
  }

  /**
   * Gets the status of a batch operation
   * 
   * @param operationId - The unique identifier of the batch operation
   * @returns Promise<BatchOperationStatusResponse> The batch operation status response
   * @throws {ConduitCoreError} When the API request fails
   * 
   * @example
   * ```typescript
   * const status = await coreClient.batchOperations.getOperationStatus('batch-123');
   * 
   * console.log(`Operation status: ${status.status}`);
   * console.log(`Progress: ${status.metadata.processedItems}/${status.metadata.totalItems}`);
   * console.log(`Success rate: ${((status.metadata.processedItems - status.metadata.failedItems) / status.metadata.processedItems * 100).toFixed(2)}%`);
   * 
   * if (status.status === BatchOperationStatusEnum.Completed) {
   *   console.log('Batch operation completed!');
   * } else if (status.status === BatchOperationStatusEnum.Failed) {
   *   console.log('Batch operation failed:', status.errors);
   * }
   * ```
   */
  async getOperationStatus(operationId: string): Promise<BatchOperationStatusResponse> {
    return this.client['request']<BatchOperationStatusResponse>({
      method: 'GET',
      url: `/v1/batch/operations/${operationId}`,
    });
  }

  /**
   * Cancels a running batch operation
   * 
   * @param operationId - The unique identifier of the batch operation to cancel
   * @returns Promise<BatchOperationStatusResponse> The updated batch operation status response
   * @throws {ConduitCoreError} When the API request fails
   * 
   * @example
   * ```typescript
   * const canceledStatus = await coreClient.batchOperations.cancelOperation('batch-123');
   * console.log(`Operation canceled. Final status: ${canceledStatus.status}`);
   * ```
   */
  async cancelOperation(operationId: string): Promise<BatchOperationStatusResponse> {
    return this.client['request']<BatchOperationStatusResponse>({
      method: 'POST',
      url: `/v1/batch/operations/${operationId}/cancel`,
      data: {},
    });
  }

  /**
   * Polls a batch operation until completion or timeout
   * 
   * @param operationId - The unique identifier of the batch operation
   * @param options - Polling options (interval and timeout)
   * @returns Promise<BatchOperationStatusResponse> The final batch operation status response
   * @throws {Error} When the operation doesn't complete within the timeout
   * 
   * @example
   * ```typescript
   * // Poll every 3 seconds for up to 5 minutes
   * const finalStatus = await coreClient.batchOperations.pollOperation('batch-123', {
   *   pollingInterval: 3000,
   *   timeout: 300000
   * });
   * 
   * if (finalStatus.status === BatchOperationStatusEnum.Completed) {
   *   console.log('Operation completed successfully!');
   *   console.log(`Processed ${finalStatus.metadata.processedItems} items`);
   * }
   * ```
   */
  async pollOperation(
    operationId: string,
    options: BatchOperationPollOptions = {}
  ): Promise<BatchOperationStatusResponse> {
    const pollingInterval = options.pollingInterval || 5000;
    const timeout = options.timeout || 600000; // 10 minutes default

    const startTime = Date.now();
    let lastStatus: BatchOperationStatusResponse;

    while (Date.now() - startTime < timeout) {
      lastStatus = await this.getOperationStatus(operationId);

      if (lastStatus.status === BatchOperationStatusEnum.Completed ||
          lastStatus.status === BatchOperationStatusEnum.Failed ||
          lastStatus.status === BatchOperationStatusEnum.Cancelled ||
          lastStatus.status === BatchOperationStatusEnum.PartiallyCompleted) {
        return lastStatus;
      }

      await new Promise(resolve => setTimeout(resolve, pollingInterval));
    }

    throw new Error(
      `Batch operation ${operationId} did not complete within ${timeout}ms. ` +
      `Last status: ${lastStatus!.status}`
    );
  }

  /**
   * Validates a batch spend update request
   * 
   * @param spendUpdates - Array of spend updates to validate
   * @param options - Validation options
   * @returns BatchValidationResult Validation result with errors and warnings
   * 
   * @example
   * ```typescript
   * const spendUpdates = [
   *   { virtualKeyId: 1, amount: 10.50, model: 'gpt-4', provider: 'openai' },
   *   { virtualKeyId: 0, amount: -5, model: '', provider: 'invalid' } // Invalid
   * ];
   * 
   * const validation = BatchOperationsService.validateSpendUpdateRequest(spendUpdates);
   * if (!validation.isValid) {
   *   console.log('Validation errors:', validation.errors);
   * }
   * ```
   */
  static validateSpendUpdateRequest(
    spendUpdates: SpendUpdateDto[], 
    options: BatchValidationOptions = {}
  ): BatchValidationResult {
    const errors: string[] = [];
    const warnings: string[] = [];
    const validateItems = options.validateItems !== false;

    if (spendUpdates.length > 10000) {
      errors.push('Cannot process more than 10,000 spend updates in a single batch');
    }

    if (spendUpdates.length === 0) {
      errors.push('Cannot create empty batch spend update request');
    }

    if (validateItems) {
      spendUpdates.forEach((update, index) => {
        if (!update.virtualKeyId || update.virtualKeyId <= 0) {
          errors.push(`Invalid virtualKeyId at index ${index}: ${update.virtualKeyId}`);
        }

        if (!update.amount || update.amount <= 0 || update.amount > 1000000) {
          errors.push(`Invalid amount at index ${index}: ${update.amount}. Must be between 0.0001 and 1,000,000`);
        }

        if (!update.model || update.model.trim() === '') {
          errors.push(`Model cannot be empty at index ${index}`);
        }

        if (!update.provider || update.provider.trim() === '') {
          errors.push(`Provider cannot be empty at index ${index}`);
        }

        if (update.amount && update.amount < 0.01) {
          warnings.push(`Small amount at index ${index}: ${update.amount}. Consider using larger amounts for better efficiency`);
        }
      });
    }

    return {
      isValid: errors.length === 0,
      errors,
      itemCount: spendUpdates.length,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  /**
   * Validates a batch virtual key update request
   * 
   * @param virtualKeyUpdates - Array of virtual key updates to validate
   * @param options - Validation options
   * @returns BatchValidationResult Validation result with errors and warnings
   */
  static validateVirtualKeyUpdateRequest(
    virtualKeyUpdates: VirtualKeyUpdateDto[], 
    options: BatchValidationOptions = {}
  ): BatchValidationResult {
    const errors: string[] = [];
    const warnings: string[] = [];
    const validateItems = options.validateItems !== false;
    const validateDates = options.validateDates !== false;

    if (virtualKeyUpdates.length > 1000) {
      errors.push('Cannot process more than 1,000 virtual key updates in a single batch');
    }

    if (virtualKeyUpdates.length === 0) {
      errors.push('Cannot create empty batch virtual key update request');
    }

    if (validateItems) {
      virtualKeyUpdates.forEach((update, index) => {
        if (!update.virtualKeyId || update.virtualKeyId <= 0) {
          errors.push(`Invalid virtualKeyId at index ${index}: ${update.virtualKeyId}`);
        }

        if (update.maxBudget !== undefined && update.maxBudget < 0) {
          errors.push(`Invalid maxBudget at index ${index}: ${update.maxBudget}. Cannot be negative`);
        }

        if (validateDates && update.expiresAt) {
          const expiryDate = new Date(update.expiresAt);
          if (isNaN(expiryDate.getTime())) {
            errors.push(`Invalid expiresAt date format at index ${index}: ${update.expiresAt}`);
          } else if (expiryDate < new Date()) {
            errors.push(`Invalid expiresAt at index ${index}: ${update.expiresAt}. Cannot be in the past`);
          }
        }

        if (update.allowedModels && update.allowedModels.length === 0) {
          warnings.push(`Empty allowedModels array at index ${index}. This will remove all model restrictions`);
        }
      });
    }

    return {
      isValid: errors.length === 0,
      errors,
      itemCount: virtualKeyUpdates.length,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  /**
   * Validates a batch webhook send request
   * 
   * @param webhookSends - Array of webhook sends to validate
   * @param options - Validation options
   * @returns BatchValidationResult Validation result with errors and warnings
   */
  static validateWebhookSendRequest(
    webhookSends: WebhookSendDto[], 
    options: BatchValidationOptions = {}
  ): BatchValidationResult {
    const errors: string[] = [];
    const warnings: string[] = [];
    const validateItems = options.validateItems !== false;
    const validateUrls = options.validateUrls !== false;

    if (webhookSends.length > 5000) {
      errors.push('Cannot process more than 5,000 webhook sends in a single batch');
    }

    if (webhookSends.length === 0) {
      errors.push('Cannot create empty batch webhook send request');
    }

    if (validateItems) {
      webhookSends.forEach((webhook, index) => {
        if (!webhook.url || webhook.url.trim() === '') {
          errors.push(`URL cannot be empty at index ${index}`);
        } else if (validateUrls) {
          try {
            const url = new URL(webhook.url);
            if (url.protocol !== 'http:' && url.protocol !== 'https:') {
              errors.push(`Invalid URL protocol at index ${index}: ${webhook.url}. Must be http or https`);
            }
          } catch {
            errors.push(`Invalid URL format at index ${index}: ${webhook.url}`);
          }
        }

        if (!webhook.eventType || webhook.eventType.trim() === '') {
          errors.push(`EventType cannot be empty at index ${index}`);
        }

        if (!webhook.payload || Object.keys(webhook.payload).length === 0) {
          errors.push(`Payload cannot be empty at index ${index}`);
        }

        if (webhook.headers && Object.keys(webhook.headers).length > 50) {
          warnings.push(`Large number of headers at index ${index}. Consider reducing for better performance`);
        }
      });
    }

    return {
      isValid: errors.length === 0,
      errors,
      itemCount: webhookSends.length,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  /**
   * Creates a validated batch spend update request
   * 
   * @param spendUpdates - Array of spend updates
   * @returns BatchSpendUpdateRequest Validated request object
   * @throws {Error} When validation fails
   */
  static createSpendUpdateRequest(spendUpdates: SpendUpdateDto[]): BatchSpendUpdateRequest {
    const validation = this.validateSpendUpdateRequest(spendUpdates);
    if (!validation.isValid) {
      throw new Error(`Batch spend update validation failed: ${validation.errors.join(', ')}`);
    }
    return { spendUpdates };
  }

  /**
   * Creates a validated batch virtual key update request
   * 
   * @param virtualKeyUpdates - Array of virtual key updates
   * @returns BatchVirtualKeyUpdateRequest Validated request object
   * @throws {Error} When validation fails
   */
  static createVirtualKeyUpdateRequest(virtualKeyUpdates: VirtualKeyUpdateDto[]): BatchVirtualKeyUpdateRequest {
    const validation = this.validateVirtualKeyUpdateRequest(virtualKeyUpdates);
    if (!validation.isValid) {
      throw new Error(`Batch virtual key update validation failed: ${validation.errors.join(', ')}`);
    }
    return { virtualKeyUpdates };
  }

  /**
   * Creates a validated batch webhook send request
   * 
   * @param webhookSends - Array of webhook sends
   * @returns BatchWebhookSendRequest Validated request object
   * @throws {Error} When validation fails
   */
  static createWebhookSendRequest(webhookSends: WebhookSendDto[]): BatchWebhookSendRequest {
    const validation = this.validateWebhookSendRequest(webhookSends);
    if (!validation.isValid) {
      throw new Error(`Batch webhook send validation failed: ${validation.errors.join(', ')}`);
    }
    return { webhookSends };
  }
}
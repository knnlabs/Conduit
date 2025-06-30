/**
 * Batch operation status enumeration
 */
export enum BatchOperationStatusEnum {
  Queued = 'Queued',
  Running = 'Running', 
  Completed = 'Completed',
  Failed = 'Failed',
  Cancelled = 'Cancelled',
  PartiallyCompleted = 'PartiallyCompleted'
}

/**
 * Individual spend update item for batch operations
 */
export interface SpendUpdateDto {
  /** Virtual key ID to update spend for */
  virtualKeyId: number;
  /** Amount to add to the spend (0.0001 to 1,000,000) */
  amount: number;
  /** Model name associated with the spend */
  model: string;
  /** Provider name associated with the spend */
  provider: string;
  /** Optional metadata for the spend update */
  metadata?: Record<string, unknown>;
}

/**
 * Request for batch spend updates (max 10,000 items)
 */
export interface BatchSpendUpdateRequest {
  /** List of spend updates to process */
  spendUpdates: SpendUpdateDto[];
}

/**
 * Individual virtual key update item for batch operations
 */
export interface VirtualKeyUpdateDto {
  /** Virtual key ID to update */
  virtualKeyId: number;
  /** New maximum budget for the virtual key */
  maxBudget?: number;
  /** New list of allowed models */
  allowedModels?: string[];
  /** New rate limits configuration */
  rateLimits?: Record<string, unknown>;
  /** Whether the virtual key is enabled */
  isEnabled?: boolean;
  /** New expiration date for the virtual key */
  expiresAt?: string;
  /** New notes for the virtual key */
  notes?: string;
}

/**
 * Request for batch virtual key updates (max 1,000 items, requires admin permissions)
 */
export interface BatchVirtualKeyUpdateRequest {
  /** List of virtual key updates to process */
  virtualKeyUpdates: VirtualKeyUpdateDto[];
}

/**
 * Individual webhook send item for batch operations
 */
export interface WebhookSendDto {
  /** Webhook URL to send to */
  url: string;
  /** Event type for the webhook */
  eventType: string;
  /** Payload to send in the webhook */
  payload: Record<string, unknown>;
  /** Optional headers to include in the webhook request */
  headers?: Record<string, string>;
  /** Optional secret for webhook signature verification */
  secret?: string;
}

/**
 * Request for batch webhook sends (max 5,000 items)
 */
export interface BatchWebhookSendRequest {
  /** List of webhook sends to process */
  webhookSends: WebhookSendDto[];
}

/**
 * Response when starting a batch operation
 */
export interface BatchOperationStartResponse {
  /** Unique identifier for the batch operation */
  operationId: string;
  /** Task ID for SignalR real-time updates */
  taskId: string;
  /** URL to check operation status */
  statusUrl: string;
  /** Current operation status */
  status: BatchOperationStatusEnum;
  /** When the operation was started */
  startedAt: string;
}

/**
 * Batch operation progress metadata
 */
export interface BatchOperationMetadata {
  /** Total number of items in the batch */
  totalItems: number;
  /** Number of successfully processed items */
  processedItems: number;
  /** Number of failed items */
  failedItems: number;
  /** Processing rate in items per second */
  itemsPerSecond: number;
  /** Estimated time of completion */
  estimatedCompletion?: string;
  /** Operation start time */
  startedAt: string;
  /** Operation completion time */
  completedAt?: string;
}

/**
 * Individual batch item processing result
 */
export interface BatchItemResult {
  /** Item index in the batch */
  index: number;
  /** Whether the item was processed successfully */
  success: boolean;
  /** Error message if processing failed */
  errorMessage?: string;
  /** Error code if processing failed */
  errorCode?: string;
  /** Processing timestamp */
  processedAt: string;
}

/**
 * Batch item error details
 */
export interface BatchItemError {
  /** Item index in the batch */
  index: number;
  /** Error message */
  message: string;
  /** Error code */
  code?: string;
  /** Exception type if available */
  exceptionType?: string;
  /** Timestamp when error occurred */
  timestamp: string;
}

/**
 * Response containing batch operation status and results
 */
export interface BatchOperationStatusResponse {
  /** Unique identifier for the batch operation */
  operationId: string;
  /** Current operation status */
  status: BatchOperationStatusEnum;
  /** Progress metadata */
  metadata: BatchOperationMetadata;
  /** List of individual item results */
  results: BatchItemResult[];
  /** List of errors that occurred during processing */
  errors: BatchItemError[];
  /** Whether the operation can be cancelled */
  canCancel: boolean;
  /** Additional operation details */
  details?: Record<string, unknown>;
}

/**
 * Options for batch operation polling
 */
export interface BatchOperationPollOptions {
  /** How often to check the status (default: 5 seconds) */
  pollingInterval?: number;
  /** Maximum time to wait for completion in milliseconds (default: 10 minutes) */
  timeout?: number;
}

/**
 * Validation options for batch operations
 */
export interface BatchValidationOptions {
  /** Whether to validate individual items (default: true) */
  validateItems?: boolean;
  /** Whether to validate URL formats for webhooks (default: true) */
  validateUrls?: boolean;
  /** Whether to validate date formats (default: true) */
  validateDates?: boolean;
}

/**
 * Result of batch operation validation
 */
export interface BatchValidationResult {
  /** Whether validation passed */
  isValid: boolean;
  /** List of validation errors */
  errors: string[];
  /** Number of items validated */
  itemCount: number;
  /** Validation warnings (non-blocking) */
  warnings?: string[];
}
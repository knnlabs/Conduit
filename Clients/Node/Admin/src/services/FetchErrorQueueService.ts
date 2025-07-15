import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';

export interface ErrorQueueInfo {
  queueName: string;
  originalQueue: string;
  messageCount: number;
  messageBytes: number;
  consumerCount: number;
  oldestMessageTimestamp?: string;
  newestMessageTimestamp?: string;
  messageRate: number;
  status: 'ok' | 'warning' | 'critical';
}

export interface ErrorQueueSummary {
  totalQueues: number;
  totalMessages: number;
  totalBytes: number;
  criticalQueues: string[];
  warningQueues: string[];
}

export interface ErrorQueueListResponse {
  queues: ErrorQueueInfo[];
  summary: ErrorQueueSummary;
  timestamp: string;
}

export interface ErrorMessage {
  messageId: string;
  correlationId: string;
  timestamp: string;
  messageType: string;
  headers: Record<string, any>;
  body?: any;
  error: ErrorDetails;
  retryCount: number;
}

export interface ErrorMessageDetail extends ErrorMessage {
  context: Record<string, any>;
  fullException?: string;
}

export interface ErrorDetails {
  exceptionType: string;
  message: string;
  stackTrace?: string;
  failedAt: string;
}

export interface ErrorMessageListResponse {
  queueName: string;
  messages: ErrorMessage[];
  page: number;
  pageSize: number;
  totalMessages: number;
  totalPages: number;
}

export interface ErrorRateTrend {
  period: string;
  errorCount: number;
  errorsPerMinute: number;
}

export interface FailingMessageType {
  messageType: string;
  failureCount: number;
  percentage: number;
  mostCommonError: string;
}

export interface QueueGrowthPattern {
  queueName: string;
  growthRate: number;
  trend: 'increasing' | 'decreasing' | 'stable';
  currentCount: number;
}

export interface ErrorQueueStatistics {
  since: string;
  until: string;
  groupBy: string;
  errorRateTrends: ErrorRateTrend[];
  topFailingMessageTypes: FailingMessageType[];
  queueGrowthPatterns: QueueGrowthPattern[];
  averageMessageAgeHours: number;
  totalErrors: number;
}

export interface HealthStatusCounts {
  healthy: number;
  warning: number;
  critical: number;
}

export interface HealthIssue {
  severity: 'warning' | 'critical';
  queueName: string;
  description: string;
  suggestedAction?: string;
}

export interface ErrorQueueHealth {
  status: 'healthy' | 'degraded' | 'unhealthy';
  timestamp: string;
  statusCounts: HealthStatusCounts;
  issues: HealthIssue[];
  healthScore: number;
}

/**
 * Response from clearing all messages in a queue
 */
export interface QueueClearResponse {
  success: boolean;
  message: string;
  deletedCount: number;
}

/**
 * Response from replaying messages
 */
export interface MessageReplayResponse {
  success: boolean;
  message: string;
  successCount: number;
  failedCount: number;
}

/**
 * Response from deleting a specific message
 */
export interface MessageDeleteResponse {
  success: boolean;
  message: string;
  deletedCount: number;
}

/**
 * Type-safe Error Queue service using native fetch
 */
export class FetchErrorQueueService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all error queues with optional filters
   */
  async getErrorQueues(
    options?: {
      includeEmpty?: boolean;
      minMessages?: number;
      queueNameFilter?: string;
    },
    config?: RequestConfig
  ): Promise<ErrorQueueListResponse> {
    const params = new URLSearchParams();
    if (options?.includeEmpty !== undefined) {
      params.append('includeEmpty', options.includeEmpty.toString());
    }
    if (options?.minMessages !== undefined) {
      params.append('minMessages', options.minMessages.toString());
    }
    if (options?.queueNameFilter) {
      params.append('queueNameFilter', options.queueNameFilter);
    }

    return this.client['get']<ErrorQueueListResponse>(
      `/api/admin/error-queues${params.toString() ? `?${params.toString()}` : ''}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get messages from a specific error queue
   */
  async getErrorMessages(
    queueName: string,
    options?: {
      page?: number;
      pageSize?: number;
      includeHeaders?: boolean;
      includeBody?: boolean;
    },
    config?: RequestConfig
  ): Promise<ErrorMessageListResponse> {
    const params = new URLSearchParams();
    if (options?.page !== undefined) {
      params.append('page', options.page.toString());
    }
    if (options?.pageSize !== undefined) {
      params.append('pageSize', options.pageSize.toString());
    }
    if (options?.includeHeaders !== undefined) {
      params.append('includeHeaders', options.includeHeaders.toString());
    }
    if (options?.includeBody !== undefined) {
      params.append('includeBody', options.includeBody.toString());
    }

    return this.client['get']<ErrorMessageListResponse>(
      `/api/admin/error-queues/${encodeURIComponent(queueName)}/messages${params.toString() ? `?${params.toString()}` : ''}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get details of a specific error message
   */
  async getErrorMessage(
    queueName: string,
    messageId: string,
    config?: RequestConfig
  ): Promise<ErrorMessageDetail> {
    return this.client['get']<ErrorMessageDetail>(
      `/api/admin/error-queues/${encodeURIComponent(queueName)}/messages/${encodeURIComponent(messageId)}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get aggregated statistics and trends for error queues
   */
  async getStatistics(
    options?: {
      since?: Date;
      groupBy?: 'hour' | 'day' | 'week';
    },
    config?: RequestConfig
  ): Promise<ErrorQueueStatistics> {
    const params = new URLSearchParams();
    if (options?.since) {
      params.append('since', options.since.toISOString());
    }
    if (options?.groupBy) {
      params.append('groupBy', options.groupBy);
    }

    return this.client['get']<ErrorQueueStatistics>(
      `/api/admin/error-queues/statistics${params.toString() ? `?${params.toString()}` : ''}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get health status of error queues for monitoring systems
   */
  async getHealth(config?: RequestConfig): Promise<ErrorQueueHealth> {
    return this.client['get']<ErrorQueueHealth>(
      '/api/admin/error-queues/health',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Clear all messages from an error queue
   * @param queueName - Name of the error queue to clear
   * @param config - Optional request configuration
   * @returns Response with the number of deleted messages
   */
  async clearQueue(queueName: string, config?: RequestConfig): Promise<QueueClearResponse> {
    return this.client['delete']<QueueClearResponse>(
      `/api/admin/error-queues/${encodeURIComponent(queueName)}/messages`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Replay a specific failed message
   * @param queueName - Name of the error queue
   * @param messageId - ID of the message to replay
   * @param config - Optional request configuration
   * @returns Response with replay operation results
   */
  async replayMessage(queueName: string, messageId: string, config?: RequestConfig): Promise<MessageReplayResponse> {
    return this.client['post']<MessageReplayResponse, { messageIds: string[] }>(
      `/api/admin/error-queues/${encodeURIComponent(queueName)}/replay`,
      { messageIds: [messageId] },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Replay all messages in a queue or specific messages if IDs provided
   * @param queueName - Name of the error queue
   * @param messageIds - Optional array of message IDs to replay. If not provided, all messages are replayed
   * @param config - Optional request configuration
   * @returns Response with replay operation results
   */
  async replayAllMessages(queueName: string, messageIds?: string[], config?: RequestConfig): Promise<MessageReplayResponse> {
    const body = messageIds?.length ? { messageIds } : {};
    
    return this.client['post']<MessageReplayResponse, { messageIds?: string[] }>(
      `/api/admin/error-queues/${encodeURIComponent(queueName)}/replay`,
      body,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a specific message from an error queue
   * @param queueName - Name of the error queue
   * @param messageId - ID of the message to delete
   * @param config - Optional request configuration
   * @returns Response with deletion results
   */
  async deleteMessage(queueName: string, messageId: string, config?: RequestConfig): Promise<MessageDeleteResponse> {
    return this.client['delete']<MessageDeleteResponse>(
      `/api/admin/error-queues/${encodeURIComponent(queueName)}/messages/${encodeURIComponent(messageId)}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}
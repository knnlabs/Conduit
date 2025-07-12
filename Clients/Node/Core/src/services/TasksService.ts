import type { FetchBasedClient } from '../client/FetchBasedClient';
import { HttpMethod } from '../client/HttpMethod';
import type { RequestOptions } from '../client/types';
import { ConduitError } from '../utils/errors';
import { API_ENDPOINTS, TASK_STATUS } from '../constants';

/**
 * Service for general task management operations using the Conduit Core API
 */
export class TasksService {

  constructor(private readonly client: FetchBasedClient) {}

  /**
   * Gets the status of any task by its ID
   */
  async getTaskStatus(
    taskId: string,
    options?: RequestOptions
  ): Promise<TaskStatusResponse> {
    try {
      if (!taskId || taskId.trim().length === 0) {
        throw new Error('Task ID is required');
      }

      const response = await this.client['request']<TaskStatusResponse>(
        API_ENDPOINTS.V1.TASKS.BY_ID(taskId),
        {
          method: HttpMethod.GET,
          ...options
        }
      );

      return response;
    } catch (error) {
      if (error instanceof ConduitError) {
        throw error;
      }
      throw new ConduitError(
        `Failed to get task status: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }

  /**
   * Cancels a pending or running task
   */
  async cancelTask(
    taskId: string,
    options?: RequestOptions
  ): Promise<void> {
    try {
      if (!taskId || taskId.trim().length === 0) {
        throw new Error('Task ID is required');
      }

      await this.client['request']<void>(
        API_ENDPOINTS.V1.TASKS.CANCEL(taskId),
        {
          method: HttpMethod.POST,
          body: {},
          ...options
        }
      );
    } catch (error) {
      if (error instanceof ConduitError) {
        throw error;
      }
      throw new ConduitError(
        `Failed to cancel task: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }

  /**
   * Polls a task until completion or timeout
   */
  async pollTaskUntilCompletion<T = any>(
    taskId: string,
    pollingOptions?: TaskPollingOptions,
    options?: RequestOptions
  ): Promise<T> {
    const opts: Required<TaskPollingOptions> = {
      intervalMs: pollingOptions?.intervalMs ?? TaskDefaults.POLLING_INTERVAL_MS,
      timeoutMs: pollingOptions?.timeoutMs ?? TaskDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: pollingOptions?.useExponentialBackoff ?? true,
      maxIntervalMs: pollingOptions?.maxIntervalMs ?? TaskDefaults.MAX_POLLING_INTERVAL_MS,
    };

    if (!taskId || taskId.trim().length === 0) {
      throw new Error('Task ID is required');
    }

    const startTime = Date.now();
    let currentInterval = opts.intervalMs;

    while (true) {
      // Check timeout
      if (Date.now() - startTime > opts.timeoutMs) {
        throw new ConduitError(
          `Task polling timed out after ${opts.timeoutMs}ms`
        );
      }

      const status = await this.getTaskStatus(taskId, options);

      switch (status.status?.toLowerCase()) {
        case TASK_STATUS.COMPLETED:
          if (!status.result) {
            throw new ConduitError(
              'Task completed but no result was provided'
            );
          }
          return status.result as T;

        case TASK_STATUS.FAILED:
          throw new ConduitError(
            `Task failed: ${status.error || 'Unknown error'}`
          );

        case TASK_STATUS.CANCELLED:
          throw new ConduitError('Task was cancelled');

        case TASK_STATUS.TIMEDOUT:
          throw new ConduitError('Task timed out');

        case TASK_STATUS.PENDING:
        case TASK_STATUS.RUNNING:
          // Continue polling
          break;

        default:
          throw new ConduitError(
            `Unknown task status: ${status.status}`
          );
      }

      // Wait before next poll
      await this.sleep(currentInterval);

      // Apply exponential backoff if enabled
      if (opts.useExponentialBackoff) {
        currentInterval = Math.min(currentInterval * 2, opts.maxIntervalMs);
      }
    }
  }

  /**
   * Requests cleanup of old completed tasks (admin operation)
   */
  async cleanupOldTasks(
    olderThanHours: number = 24,
    options?: RequestOptions
  ): Promise<number> {
    try {
      const request = { older_than_hours: olderThanHours };

      const response = await this.client['request']<CleanupTasksResponse>(
        API_ENDPOINTS.V1.TASKS.CLEANUP,
        {
          method: HttpMethod.POST,
          body: request,
          ...options
        }
      );

      return response.tasks_removed;
    } catch (error) {
      if (error instanceof ConduitError) {
        throw error;
      }
      throw new ConduitError(
        `Failed to cleanup old tasks: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }

  /**
   * Helper method to sleep for a specified duration
   */
  private sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}

/**
 * Response from a general task status request
 */
export interface TaskStatusResponse {
  /** The unique task identifier */
  task_id: string;

  /** The current status of the task */
  status?: string;

  /** The task type */
  task_type?: string;

  /** The progress percentage (0-100) */
  progress: number;

  /** An optional progress message */
  message?: string;

  /** When the task was created */
  created_at: string;

  /** When the task was last updated */
  updated_at: string;

  /** The task result, available when status is completed */
  result?: any;

  /** Error information if the task failed */
  error?: string;
}

/**
 * Options for polling task status
 */
export interface TaskPollingOptions {
  /** The polling interval in milliseconds */
  intervalMs?: number;

  /** The maximum polling timeout in milliseconds */
  timeoutMs?: number;

  /** Whether to use exponential backoff for polling intervals */
  useExponentialBackoff?: boolean;

  /** The maximum interval between polls in milliseconds when using exponential backoff */
  maxIntervalMs?: number;
}

/**
 * Response from a cleanup tasks request
 */
export interface CleanupTasksResponse {
  /** The number of tasks that were removed */
  tasks_removed: number;
}

/**
 * Default values for task operations
 */
export const TaskDefaults = {
  /** Default polling interval in milliseconds */
  POLLING_INTERVAL_MS: 2000,

  /** Default polling timeout in milliseconds */
  POLLING_TIMEOUT_MS: 600000, // 10 minutes

  /** Default maximum polling interval in milliseconds */
  MAX_POLLING_INTERVAL_MS: 30000, // 30 seconds
} as const;

/**
 * Helper functions for task management
 */
export const TaskHelpers = {
  /**
   * Creates polling options with sensible defaults
   */
  createPollingOptions(
    options?: Partial<TaskPollingOptions>
  ): TaskPollingOptions {
    return {
      intervalMs: options?.intervalMs ?? TaskDefaults.POLLING_INTERVAL_MS,
      timeoutMs: options?.timeoutMs ?? TaskDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: options?.useExponentialBackoff ?? true,
      maxIntervalMs: options?.maxIntervalMs ?? TaskDefaults.MAX_POLLING_INTERVAL_MS,
    };
  },
};
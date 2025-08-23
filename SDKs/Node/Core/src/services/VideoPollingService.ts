import type { IFetchBasedClientAdapter } from '../client/ClientAdapter';
import {
  VideoTaskStatus,
  VideoDefaults,
  type AsyncVideoGenerationResponse,
  type VideoGenerationResponse,
  type VideoTaskPollingOptions,
} from '../models/videos';
import { ConduitError } from '../utils/errors';

/**
 * Service for video task polling operations
 */
export class VideoPollingService {
  constructor(private readonly clientAdapter: IFetchBasedClientAdapter) {}

  /**
   * Gets the status of an async video generation task
   */
  async getTaskStatus(
    taskId: string,
    options?: { signal?: AbortSignal }
  ): Promise<AsyncVideoGenerationResponse> {
    try {
      if (!taskId || taskId.trim().length === 0) {
        throw new Error('Task ID is required');
      }

      const endpoint = `/v1/videos/generations/tasks/${encodeURIComponent(taskId)}`;

      const response = await this.clientAdapter.get<AsyncVideoGenerationResponse>(
        endpoint,
        options
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
   * Cancels a pending or running async video generation task
   */
  async cancelTask(
    taskId: string,
    options?: { signal?: AbortSignal }
  ): Promise<void> {
    try {
      if (!taskId || taskId.trim().length === 0) {
        throw new Error('Task ID is required');
      }

      const endpoint = `/v1/videos/generations/${encodeURIComponent(taskId)}`;

      await this.clientAdapter.delete<void>(
        endpoint,
        options
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
   * Polls an async video generation task until completion or timeout
   */
  async pollTaskUntilCompletion(
    taskId: string,
    pollingOptions?: VideoTaskPollingOptions,
    options?: { signal?: AbortSignal }
  ): Promise<VideoGenerationResponse> {
    const opts: Required<Omit<VideoTaskPollingOptions, 'onProgress' | 'onStarted' | 'onCompleted' | 'onFailed'>> = {
      intervalMs: pollingOptions?.intervalMs ?? VideoDefaults.POLLING_INTERVAL_MS,
      timeoutMs: pollingOptions?.timeoutMs ?? VideoDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: pollingOptions?.useExponentialBackoff ?? true,
      maxIntervalMs: pollingOptions?.maxIntervalMs ?? VideoDefaults.MAX_POLLING_INTERVAL_MS,
    };

    if (!taskId || taskId.trim().length === 0) {
      throw new Error('Task ID is required');
    }

    const startTime = Date.now();
    let currentInterval = opts.intervalMs;
    let lastStatus: VideoTaskStatus | undefined;
    let lastProgress = -1;

    // Poll until task completes or fails
    for (;;) {
      // Check if operation was cancelled
      if (options?.signal?.aborted) {
        throw new ConduitError('Operation was cancelled');
      }

      // Check timeout
      if (Date.now() - startTime > opts.timeoutMs) {
        throw new ConduitError(
          `Task polling timed out after ${opts.timeoutMs}ms`
        );
      }

      let status: AsyncVideoGenerationResponse;
      try {
        status = await this.getTaskStatus(taskId, options);
      } catch (error) {
        // If callback errors should be isolated, wrap in try-catch
        if (pollingOptions?.onFailed) {
          try {
            pollingOptions.onFailed(
              error instanceof Error ? error.message : String(error),
              false
            );
          } catch (callbackError) {
            console.error('Error in onFailed callback:', callbackError);
          }
        }
        throw error;
      }

      // Fire onStarted callback when status changes from Pending to Running
      if (lastStatus === VideoTaskStatus.Pending && status.status === VideoTaskStatus.Running) {
        if (pollingOptions?.onStarted) {
          try {
            pollingOptions.onStarted(status.estimated_time_to_completion);
          } catch (callbackError) {
            console.error('Error in onStarted callback:', callbackError);
          }
        }
      }

      // Fire onProgress callback if progress or status changed
      if (status.progress !== lastProgress || status.status !== lastStatus) {
        lastProgress = status.progress;
        lastStatus = status.status;
        
        if (pollingOptions?.onProgress) {
          try {
            pollingOptions.onProgress(status.progress, status.status, status.message);
          } catch (callbackError) {
            console.error('Error in onProgress callback:', callbackError);
          }
        }
      }

      switch (status.status) {
        case VideoTaskStatus.Completed:
          if (!status.result) {
            throw new ConduitError(
              'Task completed but no result was provided'
            );
          }

          // Fire onCompleted callback
          if (pollingOptions?.onCompleted) {
            try {
              pollingOptions.onCompleted(status.result);
            } catch (callbackError) {
              console.error('Error in onCompleted callback:', callbackError);
            }
          }

          // Task completed successfully
          return status.result;

        case VideoTaskStatus.Failed: {
          const failedError = `Task failed: ${status.error ?? 'Unknown error'}`;
          if (pollingOptions?.onFailed) {
            try {
              pollingOptions.onFailed(status.error ?? 'Unknown error', false);
            } catch (callbackError) {
              console.error('Error in onFailed callback:', callbackError);
            }
          }
          throw new ConduitError(failedError);
        }

        case VideoTaskStatus.Cancelled:
          if (pollingOptions?.onFailed) {
            try {
              pollingOptions.onFailed('Task was cancelled', false);
            } catch (callbackError) {
              console.error('Error in onFailed callback:', callbackError);
            }
          }
          throw new ConduitError('Task was cancelled');

        case VideoTaskStatus.TimedOut:
          if (pollingOptions?.onFailed) {
            try {
              pollingOptions.onFailed('Task timed out', true);
            } catch (callbackError) {
              console.error('Error in onFailed callback:', callbackError);
            }
          }
          throw new ConduitError('Task timed out');

        case VideoTaskStatus.Pending:
        case VideoTaskStatus.Running:
          // Continue polling
          break;

        default:
          throw new ConduitError(
            `Unknown task status: ${status.status as unknown as string}`
          );
      }

      // Wait before next poll
      await new Promise(resolve => setTimeout(resolve, currentInterval));

      // Apply exponential backoff if enabled
      if (opts.useExponentialBackoff) {
        currentInterval = Math.min(currentInterval * 2, opts.maxIntervalMs);
      }
    }
  }
}
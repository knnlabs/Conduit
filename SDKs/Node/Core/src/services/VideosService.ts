import type { FetchBasedClient } from '../client/FetchBasedClient';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';
import type { VideoApiRequest, AsyncVideoApiRequest } from '../models/common-types';
import {
  VideoTaskStatus,
  VideoDefaults,
  VideoModels,
  VideoResponseFormats,
  validateAsyncVideoGenerationRequest,
  getVideoModelCapabilities,
  type VideoGenerationRequest,
  type VideoGenerationResponse,
  type AsyncVideoGenerationRequest,
  type AsyncVideoGenerationResponse,
  type VideoTaskPollingOptions,
  type VideoModelCapabilities
} from '../models/videos';
import { ConduitError } from '../utils/errors';
import type { VideoWebhookMetadata } from '../models/metadata';
import type { SignalRService } from './SignalRService';
import type { VideoGenerationHubClient } from '../signalr/VideoGenerationHubClient';

/**
 * Progress information for video generation
 */
export interface VideoProgress {
  /** Progress percentage (0-100) */
  percentage: number;
  /** Current status of the video generation */
  status: string;
  /** Optional human-readable message */
  message?: string;
}

/**
 * Callbacks for video generation progress tracking
 */
export interface VideoProgressCallbacks {
  /** Called when progress updates are received */
  onProgress?: (progress: VideoProgress) => void;
  /** Called when video generation starts */
  onStarted?: (taskId: string, estimatedSeconds?: number) => void;
  /** Called when video generation completes successfully */
  onCompleted?: (result: VideoGenerationResponse) => void;
  /** Called when video generation fails */
  onFailed?: (error: string, isRetryable: boolean) => void;
}

/**
 * Result of generateWithProgress method
 */
export interface GenerateWithProgressResult {
  /** The task ID for tracking the generation */
  taskId: string;
  /** Promise that resolves when generation completes */
  result: Promise<VideoGenerationResponse>;
}

/**
 * Service for video generation operations using the Conduit Core API
 */
export class VideosService {
  // Note: /v1/videos/generations endpoint does not exist - only async generation is supported
  private static readonly ASYNC_GENERATIONS_ENDPOINT = '/v1/videos/generations/async';
  private readonly clientAdapter: IFetchBasedClientAdapter;
  private signalRService?: SignalRService;
  private videoHubClient?: VideoGenerationHubClient;

  constructor(
    client: FetchBasedClient,
    signalRService?: SignalRService,
    videoHubClient?: VideoGenerationHubClient
  ) {
    this.clientAdapter = createClientAdapter(client);
    this.signalRService = signalRService;
    this.videoHubClient = videoHubClient;
  }

  // Synchronous generate method removed - endpoint does not exist

  /**
   * Generates videos asynchronously from a text prompt
   */
  async generateAsync(
    request: AsyncVideoGenerationRequest,
    options?: { signal?: AbortSignal }
  ): Promise<AsyncVideoGenerationResponse> {
    try {
      validateAsyncVideoGenerationRequest(request);

      // Convert to API request format
      const apiRequest = this.convertToAsyncApiRequest(request);

      const response = await this.clientAdapter.post<AsyncVideoGenerationResponse, AsyncVideoApiRequest>(
        VideosService.ASYNC_GENERATIONS_ENDPOINT,
        apiRequest,
        options
      );

      return response;
    } catch (error) {
      if (error instanceof ConduitError) {
        throw error;
      }
      throw new ConduitError(
        `Async video generation failed: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }

  /**
   * Generates videos with unified progress tracking through SignalR and polling
   * 
   * This method provides real-time progress updates via SignalR with automatic
   * fallback to polling if SignalR is unavailable or disconnects.
   * 
   * @param request - The video generation request
   * @param callbacks - Optional callbacks for progress tracking
   * @param options - Optional request options
   * @returns A promise with the task ID and a promise for the final result
   * 
   * @example
   * ```typescript
   * const { taskId, result } = await videosService.generateWithProgress(
   *   { prompt: "A beautiful sunset", model: "minimax-video" },
   *   {
   *     onProgress: (progress) => console.warn(`Progress: ${progress.percentage}%`),
   *     onStarted: (taskId, estimatedSeconds) => console.warn(`Started: ${taskId}`),
   *     onCompleted: (result) => console.warn('Completed:', result),
   *     onFailed: (error, isRetryable) => console.error('Failed:', error)
   *   }
   * );
   * 
   * // Access the task ID immediately
   * console.warn('Task ID:', taskId);
   * 
   * // Wait for the final result
   * const videoResult = await result;
   * ```
   */
  async generateWithProgress(
    request: AsyncVideoGenerationRequest,
    callbacks?: VideoProgressCallbacks,
    options?: { signal?: AbortSignal }
  ): Promise<GenerateWithProgressResult> {
    try {
      // Start the async generation
      const asyncResponse = await this.generateAsync(request, options);
      const taskId = asyncResponse.task_id;

      // Notify started callback
      if (callbacks?.onStarted) {
        callbacks.onStarted(taskId, asyncResponse.estimated_time_to_completion);
      }

      // Create a promise for the final result
      const resultPromise = this.trackProgressAndGetResult(
        taskId,
        callbacks,
        options
      );

      return {
        taskId,
        result: resultPromise
      };
    } catch (error) {
      // Notify failed callback for initial errors
      if (callbacks?.onFailed) {
        const errorMessage = error instanceof Error ? error.message : String(error);
        callbacks.onFailed(errorMessage, false);
      }
      throw error;
    }
  }

  /**
   * Internal method to track progress and return the final result
   */
  private async trackProgressAndGetResult(
    taskId: string,
    callbacks?: VideoProgressCallbacks,
    options?: { signal?: AbortSignal }
  ): Promise<VideoGenerationResponse> {
    // Use VideoProgressTracker if SignalR is available
    if (this.signalRService && this.videoHubClient) {
      const { VideoProgressTracker } = await import('../tracking/VideoProgressTracker');
      const tracker = new VideoProgressTracker(
        taskId,
        this,
        this.signalRService,
        this.videoHubClient,
        callbacks,
        {
          signal: options?.signal,
          timeoutMs: VideoDefaults.POLLING_TIMEOUT_MS,
          initialPollIntervalMs: VideoDefaults.POLLING_INTERVAL_MS,
          maxPollIntervalMs: VideoDefaults.MAX_POLLING_INTERVAL_MS,
          useExponentialBackoff: true
        }
      );
      
      return tracker.track();
    }
    
    // Fall back to polling-only implementation
    
    const pollingOptions: VideoTaskPollingOptions = {
      intervalMs: VideoDefaults.POLLING_INTERVAL_MS,
      timeoutMs: VideoDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: true,
      maxIntervalMs: VideoDefaults.MAX_POLLING_INTERVAL_MS
    };

    let lastStatus: string | undefined;
    let lastProgress = 0;

    try {
      // Poll with progress tracking
      const startTime = Date.now();
      let currentInterval = pollingOptions.intervalMs ?? VideoDefaults.POLLING_INTERVAL_MS;

      for (;;) {
        if (options?.signal?.aborted) {
          throw new ConduitError('Operation was cancelled');
        }

        if (Date.now() - startTime > (pollingOptions.timeoutMs ?? VideoDefaults.POLLING_TIMEOUT_MS)) {
          throw new ConduitError(`Task polling timed out`);
        }

        const status = await this.getTaskStatus(taskId, options);

        // Update progress if changed
        if (status.progress !== lastProgress || status.status !== lastStatus) {
          lastProgress = status.progress;
          lastStatus = status.status;

          if (callbacks?.onProgress) {
            callbacks.onProgress({
              percentage: status.progress,
              status: status.status,
              message: status.message
            });
          }
        }

        switch (status.status) {
          case VideoTaskStatus.Completed:
            if (!status.result) {
              throw new ConduitError('Task completed but no result was provided');
            }
            if (callbacks?.onCompleted) {
              callbacks.onCompleted(status.result);
            }
            return status.result;

          case VideoTaskStatus.Failed: {
            const errorMessage = status.error ?? 'Unknown error';
            if (callbacks?.onFailed) {
              callbacks.onFailed(errorMessage, false);
            }
            throw new ConduitError(`Task failed: ${errorMessage}`);
          }

          case VideoTaskStatus.Cancelled:
            if (callbacks?.onFailed) {
              callbacks.onFailed('Task was cancelled', false);
            }
            throw new ConduitError('Task was cancelled');

          case VideoTaskStatus.TimedOut:
            if (callbacks?.onFailed) {
              callbacks.onFailed('Task timed out', true);
            }
            throw new ConduitError('Task timed out');

          case VideoTaskStatus.Pending:
          case VideoTaskStatus.Running:
            // Continue polling
            break;

          default:
            throw new ConduitError(`Unknown task status: ${status.status as unknown as string}`);
        }

        await new Promise(resolve => setTimeout(resolve, currentInterval));

        if (pollingOptions.useExponentialBackoff) {
          currentInterval = Math.min(currentInterval * 2, pollingOptions.maxIntervalMs ?? VideoDefaults.MAX_POLLING_INTERVAL_MS);
        }
      }
    } catch (error) {
      if (error instanceof ConduitError) {
        throw error;
      }
      const errorMessage = error instanceof Error ? error.message : String(error);
      if (callbacks?.onFailed) {
        callbacks.onFailed(errorMessage, false);
      }
      throw new ConduitError(`Progress tracking failed: ${errorMessage}`);
    }
  }

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

    // Note: Starting to poll task

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

  /**
   * Gets the capabilities of a video model
   */
  getModelCapabilities(model: string): VideoModelCapabilities {
    return getVideoModelCapabilities(model);
  }


  /**
   * Converts a VideoGenerationRequest to the API request format
   */
  private convertToApiRequest(request: VideoGenerationRequest): VideoApiRequest {
    return {
      prompt: request.prompt,
      model: request.model ?? VideoModels.DEFAULT,
      duration: request.duration,
      size: request.size,
      fps: request.fps,
      style: request.style,
      response_format: request.response_format ?? VideoResponseFormats.URL,
      user: request.user,
      seed: request.seed,
      n: request.n ?? 1,
    };
  }

  /**
   * Converts an AsyncVideoGenerationRequest to the API request format
   */
  private convertToAsyncApiRequest(request: AsyncVideoGenerationRequest): AsyncVideoApiRequest {
    const baseRequest = this.convertToApiRequest(request);

    return {
      ...baseRequest,
      webhook_url: request.webhook_url,
      webhook_metadata: request.webhook_metadata,
      webhook_headers: request.webhook_headers,
      timeout_seconds: request.timeout_seconds,
    };
  }
}

/**
 * Helper functions for video generation
 */
export const VideoHelpers = {
  /**
   * Creates a simple video generation request
   */
  createRequest(
    prompt: string,
    options?: {
      model?: string;
      duration?: number;
      size?: string;
      fps?: number;
      style?: string;
    }
  ): VideoGenerationRequest {
    return {
      prompt,
      model: options?.model,
      duration: options?.duration,
      size: options?.size,
      fps: options?.fps,
      style: options?.style,
      n: 1,
    };
  },

  /**
   * Creates an async video generation request with webhook
   */
  createAsyncRequest(
    prompt: string,
    webhookUrl?: string,
    options?: {
      model?: string;
      duration?: number;
      size?: string;
      fps?: number;
      style?: string;
      timeoutSeconds?: number;
      webhookMetadata?: VideoWebhookMetadata;
    }
  ): AsyncVideoGenerationRequest {
    return {
      prompt,
      model: options?.model,
      duration: options?.duration,
      size: options?.size,
      fps: options?.fps,
      style: options?.style,
      webhook_url: webhookUrl,
      webhook_metadata: options?.webhookMetadata,
      timeout_seconds: options?.timeoutSeconds,
      n: 1,
    };
  },

  /**
   * Creates polling options with sensible defaults
   */
  createPollingOptions(
    options?: Partial<VideoTaskPollingOptions>
  ): VideoTaskPollingOptions {
    return {
      intervalMs: options?.intervalMs ?? VideoDefaults.POLLING_INTERVAL_MS,
      timeoutMs: options?.timeoutMs ?? VideoDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: options?.useExponentialBackoff ?? true,
      maxIntervalMs: options?.maxIntervalMs ?? VideoDefaults.MAX_POLLING_INTERVAL_MS,
    };
  },
};
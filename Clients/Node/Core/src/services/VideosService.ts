import type { BaseClient } from '../client/BaseClient';
import type {
  VideoGenerationRequest,
  VideoGenerationResponse,
  AsyncVideoGenerationRequest,
  AsyncVideoGenerationResponse,
  VideoTaskPollingOptions,
  VideoModelCapabilities
} from '../models/videos';
import {
  VideoTaskStatus,
  VideoDefaults,
  VideoModels,
  VideoResponseFormats,
  validateVideoGenerationRequest,
  validateAsyncVideoGenerationRequest,
  getVideoModelCapabilities
} from '../models/videos';
import { ConduitError } from '../utils/errors';

/**
 * Service for video generation operations using the Conduit Core API
 */
export class VideosService {
  private static readonly GENERATIONS_ENDPOINT = '/v1/videos/generations';
  private static readonly ASYNC_GENERATIONS_ENDPOINT = '/v1/videos/generations/async';

  constructor(private readonly client: BaseClient) {}

  /**
   * Generates videos synchronously from a text prompt
   */
  async generate(
    request: VideoGenerationRequest,
    options?: { signal?: AbortSignal }
  ): Promise<VideoGenerationResponse> {
    try {
      validateVideoGenerationRequest(request);

      // Convert to API request format
      const apiRequest = this.convertToApiRequest(request);

      const response = await this.client['request']<VideoGenerationResponse>(
        {
          method: 'POST',
          url: VideosService.GENERATIONS_ENDPOINT,
          data: apiRequest,
        },
        options
      );

      return response;
    } catch (error) {
      if (error instanceof ConduitError) {
        throw error;
      }
      throw new ConduitError(
        `Video generation failed: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }

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

      const response = await this.client['request']<AsyncVideoGenerationResponse>(
        {
          method: 'POST',
          url: VideosService.ASYNC_GENERATIONS_ENDPOINT,
          data: apiRequest,
        },
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

      const response = await this.client['request']<AsyncVideoGenerationResponse>(
        {
          method: 'GET',
          url: endpoint,
        },
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

      await this.client['request']<void>(
        {
          method: 'DELETE',
          url: endpoint,
        },
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
    const opts: Required<VideoTaskPollingOptions> = {
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

    // Note: Starting to poll task

    while (true) {
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

      const status = await this.getTaskStatus(taskId, options);

      switch (status.status) {
        case VideoTaskStatus.Completed:
          if (!status.result) {
            throw new ConduitError(
              'Task completed but no result was provided'
            );
          }

          // Task completed successfully
          return status.result;

        case VideoTaskStatus.Failed:
          throw new ConduitError(
            `Task failed: ${status.error || 'Unknown error'}`
          );

        case VideoTaskStatus.Cancelled:
          throw new ConduitError('Task was cancelled');

        case VideoTaskStatus.TimedOut:
          throw new ConduitError('Task timed out');

        case VideoTaskStatus.Pending:
        case VideoTaskStatus.Running:
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
   * Gets the capabilities of a video model
   */
  getModelCapabilities(model: string): VideoModelCapabilities {
    return getVideoModelCapabilities(model);
  }

  /**
   * Helper method to sleep for a specified duration
   */
  private sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  /**
   * Converts a VideoGenerationRequest to the API request format
   */
  private convertToApiRequest(request: VideoGenerationRequest): Record<string, any> {
    return {
      prompt: request.prompt,
      model: request.model || VideoModels.DEFAULT,
      duration: request.duration,
      size: request.size,
      fps: request.fps,
      style: request.style,
      response_format: request.response_format || VideoResponseFormats.URL,
      user: request.user,
      seed: request.seed,
      n: request.n || 1,
    };
  }

  /**
   * Converts an AsyncVideoGenerationRequest to the API request format
   */
  private convertToAsyncApiRequest(request: AsyncVideoGenerationRequest): Record<string, any> {
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
      webhookMetadata?: Record<string, any>;
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
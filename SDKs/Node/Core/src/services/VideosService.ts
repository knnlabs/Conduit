import type { FetchBasedClient } from '../client/FetchBasedClient';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';
import type { VideoApiRequest, AsyncVideoApiRequest } from '../models/common-types';
import {
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
import { VideoPollingService } from './VideoPollingService';
import { VideoProgressTracker, type VideoProgress, type VideoProgressCallbacks } from './VideoProgressTracker';

/**
 * Result of generateWithProgress method
 */
export interface GenerateWithProgressResult {
  /** The task ID for tracking the generation */
  taskId: string;
  /** Promise that resolves when generation completes */
  result: Promise<VideoGenerationResponse>;
}

// Re-export types for convenience
export type { VideoProgress, VideoProgressCallbacks };

/**
 * Service for video generation operations using the Conduit Core API
 */
export class VideosService {
  // Note: /v1/videos/generations endpoint does not exist - only async generation is supported
  private static readonly ASYNC_GENERATIONS_ENDPOINT = '/v1/videos/generations/async';
  private readonly clientAdapter: IFetchBasedClientAdapter;
  private readonly pollingService: VideoPollingService;
  private signalRService?: SignalRService;
  private videoHubClient?: VideoGenerationHubClient;

  constructor(
    client: FetchBasedClient,
    signalRService?: SignalRService,
    videoHubClient?: VideoGenerationHubClient
  ) {
    this.clientAdapter = createClientAdapter(client);
    this.pollingService = new VideoPollingService(this.clientAdapter);
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
    
    // Fall back to polling-only implementation using VideoPollingService
    const pollingOptions: VideoTaskPollingOptions = {
      intervalMs: VideoDefaults.POLLING_INTERVAL_MS,
      timeoutMs: VideoDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: true,
      maxIntervalMs: VideoDefaults.MAX_POLLING_INTERVAL_MS,
      onProgress: callbacks?.onProgress ? (progress, status, message) => {
        if (callbacks.onProgress) {
          callbacks.onProgress({
            percentage: progress,
            status,
            message
          });
        }
      } : undefined,
      onStarted: callbacks?.onStarted ? (estimatedSeconds) => {
        if (callbacks.onStarted) {
          callbacks.onStarted(taskId, estimatedSeconds);
        }
      } : undefined,
      onCompleted: callbacks?.onCompleted,
      onFailed: callbacks?.onFailed
    };

    return this.pollingService.pollTaskUntilCompletion(taskId, pollingOptions, options);
  }

  // Delegate polling operations to VideoPollingService
  async getTaskStatus(...args: Parameters<VideoPollingService['getTaskStatus']>) {
    return this.pollingService.getTaskStatus(...args);
  }

  async cancelTask(...args: Parameters<VideoPollingService['cancelTask']>) {
    return this.pollingService.cancelTask(...args);
  }

  async pollTaskUntilCompletion(...args: Parameters<VideoPollingService['pollTaskUntilCompletion']>) {
    return this.pollingService.pollTaskUntilCompletion(...args);
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
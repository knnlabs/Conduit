import type { VideosService } from './VideosService';
import type { SignalRService } from './SignalRService';
import type { VideoGenerationHubClient } from '../signalr/VideoGenerationHubClient';
import {
  VideoTaskStatus,
  VideoDefaults,
  type VideoGenerationResponse,
  type VideoTaskPollingOptions,
} from '../models/videos';
import { ConduitError } from '../utils/errors';

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
 * Options for video progress tracking
 */
export interface VideoProgressOptions {
  signal?: AbortSignal;
  timeoutMs?: number;
  initialPollIntervalMs?: number;
  maxPollIntervalMs?: number;
  useExponentialBackoff?: boolean;
}

/**
 * Enhanced video progress tracker with SignalR and polling fallback
 */
export class VideoProgressTracker {
  private isSignalRConnected = false;
  private hasReceivedSignalRUpdate = false;
  private pollingTimeoutId?: NodeJS.Timeout;
  private abortController?: AbortController;

  constructor(
    private readonly taskId: string,
    private readonly videosService: VideosService,
    private readonly signalRService: SignalRService,
    private readonly videoHubClient: VideoGenerationHubClient,
    private readonly callbacks?: VideoProgressCallbacks,
    private readonly options?: VideoProgressOptions
  ) {}

  /**
   * Start tracking progress with unified SignalR and polling approach
   */
  async track(): Promise<VideoGenerationResponse> {
    // Create abort controller for this tracking session
    this.abortController = new AbortController();
    
    // Combine external signal with internal abort controller
    if (this.options?.signal) {
      this.options.signal.addEventListener('abort', () => {
        this.abortController?.abort();
      });
    }

    try {
      // Start SignalR tracking
      await this.startSignalRTracking();
      
      // Start polling as backup/fallback
      const pollingPromise = this.startPollingFallback();
      
      // Wait for either SignalR or polling to complete
      return await pollingPromise;
    } finally {
      this.cleanup();
    }
  }

  /**
   * Setup SignalR progress tracking
   */
  private async startSignalRTracking(): Promise<void> {
    try {
      // Ensure SignalR connection is established
      if ('isConnected' in this.signalRService && !this.signalRService.isConnected) {
        if ('connect' in this.signalRService) {
          await (this.signalRService as unknown as { connect: () => Promise<void> }).connect();
        }
      }
      
      this.isSignalRConnected = true;
      
      // Subscribe to video generation events for this task
      // Note: Actual SignalR implementation may vary
      if ('subscribeToVideoGeneration' in this.videoHubClient) {
        interface ProgressEvent {
          percentage: number;
          status: string;
          message?: string;
        }
        
        interface VideoHubClientWithSubscription {
          subscribeToVideoGeneration: (
            taskId: string,
            callbacks: {
              onProgress: (progress: ProgressEvent) => void;
              onCompleted: (result: VideoGenerationResponse) => void;
              onFailed: (error: string, isRetryable: boolean) => void;
            }
          ) => Promise<void>;
        }
        
        await (this.videoHubClient as unknown as VideoHubClientWithSubscription).subscribeToVideoGeneration(
          this.taskId,
          {
            onProgress: (progress: ProgressEvent) => {
              this.hasReceivedSignalRUpdate = true;
              if (this.callbacks?.onProgress) {
                this.callbacks.onProgress({
                  percentage: progress.percentage,
                  status: progress.status,
                  message: progress.message
                });
              }
            },
            onCompleted: (result: VideoGenerationResponse) => {
              this.hasReceivedSignalRUpdate = true;
              if (this.callbacks?.onCompleted) {
                this.callbacks.onCompleted(result);
              }
            },
            onFailed: (error: string, isRetryable: boolean) => {
              this.hasReceivedSignalRUpdate = true;
              if (this.callbacks?.onFailed) {
                this.callbacks.onFailed(error, isRetryable);
              }
            }
          }
        );
      }
    } catch (error) {
      console.warn('Failed to setup SignalR tracking, falling back to polling:', error);
      this.isSignalRConnected = false;
    }
  }

  /**
   * Start polling as fallback mechanism
   */
  private async startPollingFallback(): Promise<VideoGenerationResponse> {
    const pollingOptions: VideoTaskPollingOptions = {
      intervalMs: this.options?.initialPollIntervalMs ?? VideoDefaults.POLLING_INTERVAL_MS,
      timeoutMs: this.options?.timeoutMs ?? VideoDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: this.options?.useExponentialBackoff ?? true,
      maxIntervalMs: this.options?.maxPollIntervalMs ?? VideoDefaults.MAX_POLLING_INTERVAL_MS
    };

    // If SignalR is working, poll less frequently
    if (this.isSignalRConnected) {
      pollingOptions.intervalMs = Math.max((pollingOptions.intervalMs ?? VideoDefaults.POLLING_INTERVAL_MS) * 2, 10000); // At least 10s intervals
    }

    let lastStatus: string | undefined;
    let lastProgress = 0;
    const startTime = Date.now();
    let currentInterval = pollingOptions.intervalMs ?? VideoDefaults.POLLING_INTERVAL_MS;

    for (;;) {
      // Check if operation was cancelled
      if (this.abortController?.signal.aborted) {
        throw new ConduitError('Operation was cancelled');
      }

      // Check timeout
      const timeoutMs = pollingOptions.timeoutMs ?? VideoDefaults.POLLING_TIMEOUT_MS;
      if (Date.now() - startTime > timeoutMs) {
        throw new ConduitError(`Task polling timed out after ${timeoutMs}ms`);
      }

      // Get current status via polling
      const status = await this.videosService.getTaskStatus(this.taskId, {
        signal: this.abortController?.signal
      });

      // Validate status response
      if (!status) {
        throw new ConduitError('Task status response is null or undefined');
      }

      // If SignalR hasn't provided updates, use polling data for callbacks
      if (!this.hasReceivedSignalRUpdate || !this.isSignalRConnected) {
        // Update progress if changed
        const currentProgress = status.progress ?? 0;
        if (currentProgress !== lastProgress || status.status !== lastStatus) {
          lastProgress = currentProgress;
          lastStatus = status.status;

          if (this.callbacks?.onProgress) {
            this.callbacks.onProgress({
              percentage: currentProgress,
              status: status.status,
              message: status.message
            });
          }
        }
      }

      // Handle terminal states
      switch (status.status) {
        case VideoTaskStatus.Completed:
          if (!status.result) {
            throw new ConduitError('Task completed but no result was provided');
          }
          // Only call onCompleted if SignalR hasn't handled it
          if (!this.hasReceivedSignalRUpdate && this.callbacks?.onCompleted) {
            this.callbacks.onCompleted(status.result);
          }
          return status.result;

        case VideoTaskStatus.Failed: {
          const errorMessage = status.error ?? 'Unknown error';
          // Only call onFailed if SignalR hasn't handled it
          if (!this.hasReceivedSignalRUpdate && this.callbacks?.onFailed) {
            this.callbacks.onFailed(errorMessage, false);
          }
          throw new ConduitError(`Task failed: ${errorMessage}`);
        }

        case VideoTaskStatus.Cancelled:
          if (!this.hasReceivedSignalRUpdate && this.callbacks?.onFailed) {
            this.callbacks.onFailed('Task was cancelled', false);
          }
          throw new ConduitError('Task was cancelled');

        case VideoTaskStatus.TimedOut:
          if (!this.hasReceivedSignalRUpdate && this.callbacks?.onFailed) {
            this.callbacks.onFailed('Task timed out', true);
          }
          throw new ConduitError('Task timed out');

        case VideoTaskStatus.Pending:
        case VideoTaskStatus.Running:
          // Continue polling
          break;

        default:
          throw new ConduitError(`Unknown task status: ${status.status as unknown as string}`);
      }

      // Wait before next poll
      await new Promise(resolve => {
        this.pollingTimeoutId = setTimeout(resolve, currentInterval);
      });

      // Apply exponential backoff if enabled
      if (pollingOptions.useExponentialBackoff) {
        currentInterval = Math.min(currentInterval * 2, pollingOptions.maxIntervalMs ?? VideoDefaults.MAX_POLLING_INTERVAL_MS);
      }
    }
  }

  /**
   * Cleanup resources
   */
  private cleanup(): void {
    // Cancel any pending polling timeout
    if (this.pollingTimeoutId) {
      clearTimeout(this.pollingTimeoutId);
      this.pollingTimeoutId = undefined;
    }

    // Unsubscribe from SignalR events
    if (this.isSignalRConnected) {
      try {
        if ('unsubscribeFromVideoGeneration' in this.videoHubClient) {
          interface VideoHubClientWithUnsubscribe {
            unsubscribeFromVideoGeneration: (taskId: string) => Promise<void> | void;
          }
          void (this.videoHubClient as unknown as VideoHubClientWithUnsubscribe).unsubscribeFromVideoGeneration(this.taskId);
        }
      } catch (error) {
        console.warn('Failed to unsubscribe from SignalR:', error);
      }
    }

    // Abort any ongoing operations
    if (this.abortController && !this.abortController.signal.aborted) {
      this.abortController.abort();
    }
  }
}
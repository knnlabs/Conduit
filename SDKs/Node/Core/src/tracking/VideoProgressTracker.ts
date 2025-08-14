import type { VideoGenerationHubClient } from '../signalr/VideoGenerationHubClient';
import type { VideosService, VideoProgress, VideoProgressCallbacks } from '../services/VideosService';
import type { SignalRService } from '../services/SignalRService';
import { VideoTaskStatus, type VideoGenerationResponse } from '../models/videos';
import { ConduitError } from '../utils/errors';

/**
 * Options for VideoProgressTracker
 */
export interface VideoProgressTrackerOptions {
  /** Maximum time to wait for completion in milliseconds */
  timeoutMs?: number;
  /** Initial polling interval in milliseconds */
  initialPollIntervalMs?: number;
  /** Maximum polling interval in milliseconds */
  maxPollIntervalMs?: number;
  /** Whether to use exponential backoff for polling */
  useExponentialBackoff?: boolean;
  /** Time window for detecting duplicate events in milliseconds */
  deduplicationWindowMs?: number;
  /** Abort signal for cancellation */
  signal?: AbortSignal;
}

/**
 * Internal progress state for tracking
 */
interface ProgressState {
  percentage: number;
  status: string;
  message?: string;
  timestamp: number;
  source: 'signalr' | 'polling';
}

/**
 * VideoProgressTracker manages dual-mode progress tracking for video generation tasks.
 * It seamlessly switches between SignalR real-time updates and polling fallback.
 */
export class VideoProgressTracker {
  private readonly videosService: VideosService;
  private readonly signalRService: SignalRService;
  private readonly videoHubClient: VideoGenerationHubClient;
  private readonly taskId: string;
  private readonly callbacks: VideoProgressCallbacks;
  private readonly options: Required<VideoProgressTrackerOptions>;
  
  private progressHistory: ProgressState[] = [];
  private isCompleted = false;
  private isSignalRConnected = false;
  private pollingInterval?: NodeJS.Timeout;
  private signalRReconnectAttempts = 0;
  private lastPolledProgress = 0;
  private cleanupHandlers: Array<() => void> = [];

  /**
   * Default options for progress tracking
   */
  private static readonly DEFAULT_OPTIONS: Required<VideoProgressTrackerOptions> = {
    timeoutMs: 600000, // 10 minutes
    initialPollIntervalMs: 2000, // 2 seconds
    maxPollIntervalMs: 30000, // 30 seconds
    useExponentialBackoff: true,
    deduplicationWindowMs: 500, // 500ms window for duplicate detection
    signal: undefined as unknown as AbortSignal
  };

  constructor(
    taskId: string,
    videosService: VideosService,
    signalRService: SignalRService,
    videoHubClient: VideoGenerationHubClient,
    callbacks: VideoProgressCallbacks = {},
    options: VideoProgressTrackerOptions = {}
  ) {
    this.taskId = taskId;
    this.videosService = videosService;
    this.signalRService = signalRService;
    this.videoHubClient = videoHubClient;
    this.callbacks = callbacks;
    this.options = {
      ...VideoProgressTracker.DEFAULT_OPTIONS,
      ...options
    };
  }

  /**
   * Start tracking progress for the video generation task
   */
  async track(): Promise<VideoGenerationResponse> {
    const startTime = Date.now();

    try {
      // Set up SignalR connection and event handlers
      await this.setupSignalRConnection();
      
      // Start polling as fallback
      this.startPolling();

      // Wait for completion
      return await this.waitForCompletion(startTime);
    } finally {
      this.cleanup();
    }
  }

  /**
   * Set up SignalR connection and event handlers
   */
  private async setupSignalRConnection(): Promise<void> {
    try {
      // Ensure SignalR is connected
      if (!this.signalRService.isConnected()) {
        await this.signalRService.connect();
      }

      // Subscribe to the task
      await this.videoHubClient.subscribeToTask(this.taskId);
      this.isSignalRConnected = true;

      // Set up event handlers
      this.setupSignalREventHandlers();

      // Set up reconnection handler
      // Note: reconnection logic is handled internally by SignalR
      // This is just a placeholder for future custom reconnection logic if needed

      // Store cleanup handler
      this.cleanupHandlers.push(() => {
        this.videoHubClient.onVideoGenerationProgress = undefined;
        this.videoHubClient.onVideoGenerationCompleted = undefined;
        this.videoHubClient.onVideoGenerationFailed = undefined;
      });

    } catch (error) {
      console.error('Failed to setup SignalR connection:', error);
      this.isSignalRConnected = false;
      // Continue with polling fallback
    }
  }

  /**
   * Set up SignalR event handlers
   */
  private setupSignalREventHandlers(): void {
    // Progress handler
    this.videoHubClient.onVideoGenerationProgress = async (event) => {
      if (event.taskId === this.taskId) {
        const progress: VideoProgress = {
          percentage: event.progress,
          status: 'processing',
          message: event.message
        };
        
        this.handleProgressUpdate(progress, 'signalr');
      }
    };

    // Completion handler
    this.videoHubClient.onVideoGenerationCompleted = async (event) => {
      if (event.taskId === this.taskId) {
        this.isCompleted = true;
        
        // Fetch final result from API
        const finalStatus = await this.videosService.getTaskStatus(this.taskId, {
          signal: this.options.signal
        });
        
        if (finalStatus.result && this.callbacks.onCompleted) {
          this.callbacks.onCompleted(finalStatus.result);
        }
      }
    };

    // Failure handler
    this.videoHubClient.onVideoGenerationFailed = async (event) => {
      if (event.taskId === this.taskId) {
        this.isCompleted = true;
        
        if (this.callbacks.onFailed) {
          this.callbacks.onFailed(event.error, event.isRetryable);
        }
      }
    };
  }

  /**
   * Start polling for progress updates
   */
  private startPolling(): void {
    let currentInterval = this.options.initialPollIntervalMs;

    const poll = async () => {
      if (this.isCompleted || this.options.signal?.aborted) {
        return;
      }

      try {
        const status = await this.videosService.getTaskStatus(this.taskId, {
          signal: this.options.signal
        });

        if (!status) {
          return;
        }

        // Only process if progress changed
        if (status.progress !== this.lastPolledProgress) {
          this.lastPolledProgress = status.progress;
          
          const progress: VideoProgress = {
            percentage: status.progress,
            status: status.status,
            message: status.message
          };

          this.handleProgressUpdate(progress, 'polling');
        }

        // Check for completion states
        switch (status.status) {
          case VideoTaskStatus.Completed:
            this.isCompleted = true;
            if (status.result && this.callbacks.onCompleted) {
              this.callbacks.onCompleted(status.result);
            }
            break;
            
          case VideoTaskStatus.Failed:
            this.isCompleted = true;
            if (this.callbacks.onFailed) {
              this.callbacks.onFailed(status.error ?? 'Unknown error', false);
            }
            break;
            
          case VideoTaskStatus.Cancelled:
            this.isCompleted = true;
            if (this.callbacks.onFailed) {
              this.callbacks.onFailed('Task was cancelled', false);
            }
            break;
            
          case VideoTaskStatus.TimedOut:
            this.isCompleted = true;
            if (this.callbacks.onFailed) {
              this.callbacks.onFailed('Task timed out', true);
            }
            break;
        }

        // Apply exponential backoff
        if (this.options.useExponentialBackoff && !this.isSignalRConnected) {
          currentInterval = Math.min(currentInterval * 2, this.options.maxPollIntervalMs);
        }
      } catch (error) {
        console.error('Polling error:', error);
      }

      // Schedule next poll if not completed
      if (!this.isCompleted && !this.options.signal?.aborted) {
        this.pollingInterval = setTimeout(poll, currentInterval);
      }
    };

    // Start polling
    poll();
  }

  /**
   * Handle progress update with deduplication
   */
  private handleProgressUpdate(progress: VideoProgress, source: 'signalr' | 'polling'): void {
    const now = Date.now();
    
    // Check for duplicates within time window
    const isDuplicate = this.progressHistory.some(state => {
      const timeDiff = now - state.timestamp;
      return timeDiff < this.options.deduplicationWindowMs &&
             state.percentage === progress.percentage &&
             state.status === progress.status;
    });

    if (isDuplicate) {
      return; // Skip duplicate
    }

    // Add to history
    this.progressHistory.push({
      percentage: progress.percentage,
      status: progress.status,
      message: progress.message,
      timestamp: now,
      source
    });

    // Keep history size reasonable
    if (this.progressHistory.length > 100) {
      this.progressHistory = this.progressHistory.slice(-50);
    }

    // Notify callback
    if (this.callbacks.onProgress) {
      this.callbacks.onProgress(progress);
    }
  }

  /**
   * Fetch current state after reconnection
   */
  private async fetchCurrentState(): Promise<void> {
    try {
      const status = await this.videosService.getTaskStatus(this.taskId, {
        signal: this.options.signal
      });

      const progress: VideoProgress = {
        percentage: status.progress,
        status: status.status,
        message: status.message
      };

      this.handleProgressUpdate(progress, 'polling');
    } catch (error) {
      console.error('Failed to fetch current state:', error);
    }
  }

  /**
   * Wait for task completion
   */
  private async waitForCompletion(startTime: number): Promise<VideoGenerationResponse> {
    return new Promise((resolve, reject) => {
      let hasSettled = false;
      
      const checkCompletion = async () => {
        if (hasSettled) {
          return;
        }
        
        // Check timeout
        if (Date.now() - startTime > this.options.timeoutMs) {
          hasSettled = true;
          reject(new ConduitError('Video generation tracking timed out'));
          return;
        }

        // Check cancellation
        if (this.options.signal?.aborted) {
          hasSettled = true;
          reject(new ConduitError('Operation was cancelled'));
          return;
        }

        // Check if completed
        if (this.isCompleted) {
          try {
            const finalStatus = await this.videosService.getTaskStatus(this.taskId, {
              signal: this.options.signal
            });

            if (!finalStatus) {
              return;
            }

            if (finalStatus.status === VideoTaskStatus.Completed && finalStatus.result) {
              hasSettled = true;
              resolve(finalStatus.result);
            } else if (finalStatus.status === VideoTaskStatus.Failed) {
              hasSettled = true;
              reject(new ConduitError(`Task failed: ${finalStatus.error ?? 'Unknown error'}`));
            } else if (finalStatus.status === VideoTaskStatus.Cancelled) {
              hasSettled = true;
              reject(new ConduitError('Task was cancelled'));
            } else if (finalStatus.status === VideoTaskStatus.TimedOut) {
              hasSettled = true;
              reject(new ConduitError('Task timed out'));
            } else {
              hasSettled = true;
              reject(new ConduitError(`Unexpected task status: ${finalStatus.status}`));
            }
          } catch (error) {
            hasSettled = true;
            reject(error);
          }
          return;
        }

        // Check again after a short delay
        setTimeout(checkCompletion, 100);
      };

      checkCompletion();
    });
  }

  /**
   * Clean up resources
   */
  private cleanup(): void {
    // Clear polling interval
    if (this.pollingInterval) {
      clearTimeout(this.pollingInterval);
      this.pollingInterval = undefined;
    }

    // Unsubscribe from SignalR
    if (this.isSignalRConnected) {
      this.videoHubClient.unsubscribeFromTask(this.taskId).catch(error => {
        console.warn('Failed to unsubscribe from task:', error);
      });
    }

    // Run cleanup handlers
    this.cleanupHandlers.forEach(handler => handler());
    this.cleanupHandlers = [];
  }
}
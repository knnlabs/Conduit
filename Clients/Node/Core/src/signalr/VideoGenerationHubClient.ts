import type * as signalR from '@microsoft/signalr';
import { BaseSignalRConnection } from './BaseSignalRConnection';
import type {
  IVideoGenerationHubServer,
  VideoGenerationStartedEvent,
  VideoGenerationProgressEvent,
  VideoGenerationCompletedEvent,
  VideoGenerationFailedEvent
} from '../models/signalr';
import { 
  SignalREndpoints
} from '../models/signalr';

/**
 * SignalR client for the Video Generation Hub, providing real-time video generation notifications.
 */
export class VideoGenerationHubClient extends BaseSignalRConnection implements IVideoGenerationHubServer {
  /**
   * Gets the hub path for video generation notifications.
   */
  protected get hubPath(): string {
    return SignalREndpoints.VideoGenerationHub;
  }

  /**
   * Event handlers for video generation notifications.
   */
  onVideoGenerationStarted?: (event: VideoGenerationStartedEvent) => Promise<void>;
  onVideoGenerationProgress?: (event: VideoGenerationProgressEvent) => Promise<void>;
  onVideoGenerationCompleted?: (event: VideoGenerationCompletedEvent) => Promise<void>;
  onVideoGenerationFailed?: (event: VideoGenerationFailedEvent) => Promise<void>;

  /**
   * Configures the hub-specific event handlers.
   */
  protected configureHubHandlers(connection: signalR.HubConnection): void {
    connection.on('VideoGenerationStarted', async (taskId: string, prompt: string, estimatedSeconds: number) => {
      console.debug(`Video generation started: ${taskId}, Estimated: ${estimatedSeconds}s`);
      if (this.onVideoGenerationStarted) {
        await this.onVideoGenerationStarted({ eventType: 'VideoGenerationStarted', taskId, prompt, estimatedSeconds });
      }
    });

    connection.on('VideoGenerationProgress', async (
      taskId: string, 
      progress: number, 
      currentFrame?: number, 
      totalFrames?: number, 
      message?: string
    ) => {
      console.debug(`Video generation progress: ${taskId}, Progress: ${progress}%`);
      if (this.onVideoGenerationProgress) {
        await this.onVideoGenerationProgress({ 
          eventType: 'VideoGenerationProgress',
          taskId, 
          progress, 
          currentFrame, 
          totalFrames, 
          message 
        });
      }
    });

    connection.on('VideoGenerationCompleted', async (
      taskId: string, 
      videoUrl: string, 
      duration: number, 
      metadata: unknown
    ) => {
      console.debug(`Video generation completed: ${taskId}, Duration: ${duration}s`);
      if (this.onVideoGenerationCompleted) {
        await this.onVideoGenerationCompleted({ eventType: 'VideoGenerationCompleted', taskId, videoUrl, duration, metadata: metadata as Record<string, unknown> });
      }
    });

    connection.on('VideoGenerationFailed', async (taskId: string, error: string, isRetryable: boolean) => {
      console.debug(`Video generation failed: ${taskId}, Error: ${error}`);
      if (this.onVideoGenerationFailed) {
        await this.onVideoGenerationFailed({ eventType: 'VideoGenerationFailed', taskId, error, isRetryable, errorCode: undefined });
      }
    });
  }

  /**
   * Subscribe to notifications for a specific video generation task.
   */
  async subscribeToTask(taskId: string): Promise<void> {
    if (!taskId?.trim()) {
      throw new Error('Task ID cannot be null or empty');
    }

    await this.invoke('SubscribeToTask', taskId);
    console.debug(`Subscribed to video generation task: ${taskId}`);
  }

  /**
   * Unsubscribe from notifications for a specific video generation task.
   */
  async unsubscribeFromTask(taskId: string): Promise<void> {
    if (!taskId?.trim()) {
      throw new Error('Task ID cannot be null or empty');
    }

    await this.invoke('UnsubscribeFromTask', taskId);
    console.debug(`Unsubscribed from video generation task: ${taskId}`);
  }

  /**
   * Subscribe to multiple tasks at once.
   */
  async subscribeToTasks(taskIds: string[]): Promise<void> {
    await Promise.all(taskIds.map(taskId => this.subscribeToTask(taskId)));
  }

  /**
   * Unsubscribe from multiple tasks at once.
   */
  async unsubscribeFromTasks(taskIds: string[]): Promise<void> {
    await Promise.all(taskIds.map(taskId => this.unsubscribeFromTask(taskId)));
  }
}
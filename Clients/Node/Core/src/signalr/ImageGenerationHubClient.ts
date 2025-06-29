import * as signalR from '@microsoft/signalr';
import { BaseSignalRConnection } from './BaseSignalRConnection';
import { 
  SignalREndpoints,
  IImageGenerationHubServer,
  ImageGenerationStartedEvent,
  ImageGenerationProgressEvent,
  ImageGenerationCompletedEvent,
  TaskFailedEvent
} from '../models/signalr';

/**
 * SignalR client for the Image Generation Hub, providing real-time image generation notifications.
 */
export class ImageGenerationHubClient extends BaseSignalRConnection implements IImageGenerationHubServer {
  /**
   * Gets the hub path for image generation notifications.
   */
  protected get hubPath(): string {
    return SignalREndpoints.ImageGenerationHub;
  }

  /**
   * Event handlers for image generation notifications.
   */
  onImageGenerationStarted?: (event: ImageGenerationStartedEvent) => Promise<void>;
  onImageGenerationProgress?: (event: ImageGenerationProgressEvent) => Promise<void>;
  onImageGenerationCompleted?: (event: ImageGenerationCompletedEvent) => Promise<void>;
  onImageGenerationFailed?: (event: TaskFailedEvent) => Promise<void>;

  /**
   * Configures the hub-specific event handlers.
   */
  protected configureHubHandlers(connection: signalR.HubConnection): void {
    connection.on('ImageGenerationStarted', async (taskId: string, prompt: string, model: string) => {
      console.debug(`Image generation started: ${taskId}, Model: ${model}`);
      if (this.onImageGenerationStarted) {
        await this.onImageGenerationStarted({ taskId, prompt, model });
      }
    });

    connection.on('ImageGenerationProgress', async (taskId: string, progress: number, stage?: string) => {
      console.debug(`Image generation progress: ${taskId}, Progress: ${progress}%, Stage: ${stage}`);
      if (this.onImageGenerationProgress) {
        await this.onImageGenerationProgress({ taskId, progress, stage });
      }
    });

    connection.on('ImageGenerationCompleted', async (taskId: string, imageUrl: string, metadata: any) => {
      console.debug(`Image generation completed: ${taskId}`);
      if (this.onImageGenerationCompleted) {
        await this.onImageGenerationCompleted({ taskId, imageUrl, metadata });
      }
    });

    connection.on('ImageGenerationFailed', async (taskId: string, error: string, isRetryable: boolean) => {
      console.debug(`Image generation failed: ${taskId}, Error: ${error}`);
      if (this.onImageGenerationFailed) {
        await this.onImageGenerationFailed({ taskId, error, isRetryable });
      }
    });
  }

  /**
   * Subscribe to notifications for a specific image generation task.
   */
  async subscribeToTask(taskId: string): Promise<void> {
    if (!taskId?.trim()) {
      throw new Error('Task ID cannot be null or empty');
    }

    await this.invoke('SubscribeToTask', taskId);
    console.debug(`Subscribed to image generation task: ${taskId}`);
  }

  /**
   * Unsubscribe from notifications for a specific image generation task.
   */
  async unsubscribeFromTask(taskId: string): Promise<void> {
    if (!taskId?.trim()) {
      throw new Error('Task ID cannot be null or empty');
    }

    await this.invoke('UnsubscribeFromTask', taskId);
    console.debug(`Unsubscribed from image generation task: ${taskId}`);
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
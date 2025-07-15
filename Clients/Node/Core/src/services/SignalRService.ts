import type { BaseSignalRConnection } from '../signalr/BaseSignalRConnection';
import { TaskHubClient } from '../signalr/TaskHubClient';
import { VideoGenerationHubClient } from '../signalr/VideoGenerationHubClient';
import { ImageGenerationHubClient } from '../signalr/ImageGenerationHubClient';
import type { HubConnectionState } from '../models/signalr';

/**
 * Service for managing SignalR hub connections for real-time notifications.
 */
export class SignalRService {
  private readonly baseUrl: string;
  private readonly virtualKey: string;
  private readonly connections = new Map<string, BaseSignalRConnection>();
  private disposed = false;

  constructor(baseUrl: string, virtualKey: string) {
    this.baseUrl = baseUrl;
    this.virtualKey = virtualKey;
  }

  /**
   * Gets or creates a TaskHubClient for task progress notifications.
   */
  getTaskHubClient(): TaskHubClient {
    return this.getOrCreateConnection('TaskHubClient', 
      () => new TaskHubClient(this.baseUrl, this.virtualKey));
  }

  /**
   * Gets or creates a VideoGenerationHubClient for video generation notifications.
   */
  getVideoGenerationHubClient(): VideoGenerationHubClient {
    return this.getOrCreateConnection('VideoGenerationHubClient', 
      () => new VideoGenerationHubClient(this.baseUrl, this.virtualKey));
  }

  /**
   * Gets or creates an ImageGenerationHubClient for image generation notifications.
   */
  getImageGenerationHubClient(): ImageGenerationHubClient {
    return this.getOrCreateConnection('ImageGenerationHubClient', 
      () => new ImageGenerationHubClient(this.baseUrl, this.virtualKey));
  }

  /**
   * Gets or creates a connection of the specified type.
   */
  private getOrCreateConnection<T extends BaseSignalRConnection>(
    key: string, 
    factory: () => T
  ): T {
    const existing = this.connections.get(key);
    if (existing) {
      return existing as T;
    }

    const newConnection = factory();
    this.connections.set(key, newConnection);
    
    return newConnection;
  }

  /**
   * Starts all active hub connections.
   */
  async startAllConnections(): Promise<void> {
    const startPromises = Array.from(this.connections.values()).map(
      connection => connection.start()
    );
    await Promise.all(startPromises);
  }

  /**
   * Stops all active hub connections.
   */
  async stopAllConnections(): Promise<void> {
    const stopPromises = Array.from(this.connections.values()).map(
      connection => connection.stop()
    );
    await Promise.all(stopPromises);
  }

  /**
   * Waits for all connections to be established.
   */
  async waitForAllConnections(timeoutMs = 30000): Promise<boolean> {
    const waitPromises = Array.from(this.connections.values()).map(
      connection => connection.waitForConnection(timeoutMs)
    );
    
    try {
      const results = await Promise.all(waitPromises);
      return results.every(result => result === true);
    } catch {
      return false;
    }
  }

  /**
   * Gets the connection status for all hub connections.
   */
  getConnectionStatus(): Record<string, HubConnectionState> {
    const status: Record<string, HubConnectionState> = {};
    
    for (const [key, connection] of this.connections) {
      status[key] = connection.state;
    }
    
    return status;
  }

  /**
   * Checks if all connections are established.
   */
  areAllConnectionsEstablished(): boolean {
    return Array.from(this.connections.values()).every(
      connection => connection.isConnected
    );
  }

  /**
   * Subscribes to a task across all relevant hubs.
   */
  async subscribeToTask(taskId: string, taskType?: string): Promise<void> {
    const taskHubClient = this.getTaskHubClient();
    await taskHubClient.subscribeToTask(taskId);

    // Subscribe to specialized hubs based on task type
    if (taskType?.toLowerCase().includes('video')) {
      const videoHubClient = this.getVideoGenerationHubClient();
      await videoHubClient.subscribeToTask(taskId);
    } else if (taskType?.toLowerCase().includes('image')) {
      const imageHubClient = this.getImageGenerationHubClient();
      await imageHubClient.subscribeToTask(taskId);
    }

    console.debug(`Subscribed to task ${taskId} with type ${taskType}`);
  }

  /**
   * Unsubscribes from a task across all relevant hubs.
   */
  async unsubscribeFromTask(taskId: string, taskType?: string): Promise<void> {
    const taskHubClient = this.getTaskHubClient();
    await taskHubClient.unsubscribeFromTask(taskId);

    // Unsubscribe from specialized hubs based on task type
    if (taskType?.toLowerCase().includes('video')) {
      const videoHubClient = this.getVideoGenerationHubClient();
      await videoHubClient.unsubscribeFromTask(taskId);
    } else if (taskType?.toLowerCase().includes('image')) {
      const imageHubClient = this.getImageGenerationHubClient();
      await imageHubClient.unsubscribeFromTask(taskId);
    }

    console.debug(`Unsubscribed from task ${taskId} with type ${taskType}`);
  }

  /**
   * Disposes all SignalR connections.
   */
  async dispose(): Promise<void> {
    if (!this.disposed) {
      const disposePromises = Array.from(this.connections.values()).map(
        connection => connection.dispose()
      );
      await Promise.all(disposePromises);
      
      this.connections.clear();
      this.disposed = true;
      
      console.debug('Disposed SignalRService and all connections');
    }
  }
}
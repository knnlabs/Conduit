import type { SignalRService } from './SignalRService';
import type { TaskHubClient } from '../signalr/TaskHubClient';
import type { VideoGenerationHubClient } from '../signalr/VideoGenerationHubClient';
import type { ImageGenerationHubClient } from '../signalr/ImageGenerationHubClient';
import type {
  VideoProgressEvent,
  ImageProgressEvent,
  TaskUpdateEvent,
  VideoProgressCallback,
  ImageProgressCallback,
  SpendUpdateCallback,
  SpendLimitAlertCallback,
  TaskUpdateCallback,
  NotificationSubscription,
  NotificationOptions,
} from '../models/notifications';
import { HubConnectionState } from '../models/signalr';

/**
 * Service for managing real-time notifications through SignalR
 */
export class NotificationsService {
  private signalRService: SignalRService;
  private subscriptions: Map<string, NotificationSubscription> = new Map();
  private taskHubClient?: TaskHubClient;
  private videoHubClient?: VideoGenerationHubClient;
  private imageHubClient?: ImageGenerationHubClient;
  private connectionStateCallbacks: Set<(state: 'connected' | 'disconnected' | 'reconnecting') => void> = new Set();

  // Store callbacks for each subscription
  private videoCallbacks: Map<string, VideoProgressCallback> = new Map();
  private imageCallbacks: Map<string, ImageProgressCallback> = new Map();
  private spendUpdateCallbacks: Map<string, SpendUpdateCallback> = new Map();
  private spendLimitCallbacks: Map<string, SpendLimitAlertCallback> = new Map();
  private taskCallbacks: Map<string, { taskId: string; callback: TaskUpdateCallback }> = new Map();

  constructor(signalRService: SignalRService) {
    this.signalRService = signalRService;
  }

  /**
   * Subscribe to video generation progress events
   */
  onVideoProgress(
    callback: VideoProgressCallback,
    options?: NotificationOptions
  ): NotificationSubscription {
    // Ensure video hub client is initialized
    if (!this.videoHubClient) {
      this.videoHubClient = this.signalRService.getVideoGenerationHubClient();
      
      // Set up event handlers
      this.videoHubClient.onVideoGenerationProgress = (event) => {
        // Notify all video progress callbacks
        for (const [subId, cb] of this.videoCallbacks) {
          const subscription = this.subscriptions.get(subId);
          if (!subscription) continue;
          
          // Apply filters if any
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          
          const notificationEvent: VideoProgressEvent = {
            taskId: event.taskId,
            progress: event.progress,
            status: 'processing',
            message: event.message,
          };
          
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
      
      this.videoHubClient.onVideoGenerationCompleted = (event) => {
        // Notify all video progress callbacks
        for (const [subId, cb] of this.videoCallbacks) {
          const subscription = this.subscriptions.get(subId);
          if (!subscription) continue;
          
          // Apply filters if any
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          
          const notificationEvent: VideoProgressEvent = {
            taskId: event.taskId,
            progress: 100,
            status: 'completed',
            metadata: event.metadata,
          };
          
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
      
      this.videoHubClient.onVideoGenerationFailed = (event) => {
        // Notify all video progress callbacks
        for (const [subId, cb] of this.videoCallbacks) {
          const subscription = this.subscriptions.get(subId);
          if (!subscription) continue;
          
          // Apply filters if any
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          
          const notificationEvent: VideoProgressEvent = {
            taskId: event.taskId,
            progress: 0,
            status: 'failed',
            message: event.error,
          };
          
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
    }

    const subscriptionId = this.generateSubscriptionId();
    this.videoCallbacks.set(subscriptionId, callback);

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'videoProgress',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    // Handle connection state changes
    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Subscribe to image generation progress events
   */
  onImageProgress(
    callback: ImageProgressCallback,
    options?: NotificationOptions
  ): NotificationSubscription {
    // Ensure image hub client is initialized
    if (!this.imageHubClient) {
      this.imageHubClient = this.signalRService.getImageGenerationHubClient();
      
      // Set up event handlers
      this.imageHubClient.onImageGenerationProgress = (event) => {
        // Notify all image progress callbacks
        for (const [subId, cb] of this.imageCallbacks) {
          const subscription = this.subscriptions.get(subId);
          if (!subscription) continue;
          
          // Apply filters if any
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          
          const notificationEvent: ImageProgressEvent = {
            taskId: event.taskId,
            progress: event.progress,
            status: 'processing',
          };
          
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
      
      this.imageHubClient.onImageGenerationCompleted = (event) => {
        // Notify all image progress callbacks
        for (const [subId, cb] of this.imageCallbacks) {
          const subscription = this.subscriptions.get(subId);
          if (!subscription) continue;
          
          // Apply filters if any
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          
          const notificationEvent: ImageProgressEvent = {
            taskId: event.taskId,
            progress: 100,
            status: 'completed',
            images: event.imageUrl ? [{ url: event.imageUrl }] : undefined,
          };
          
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
      
      this.imageHubClient.onImageGenerationFailed = (event) => {
        // Notify all image progress callbacks
        for (const [subId, cb] of this.imageCallbacks) {
          const subscription = this.subscriptions.get(subId);
          if (!subscription) continue;
          
          // Apply filters if any
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          
          const notificationEvent: ImageProgressEvent = {
            taskId: event.taskId,
            progress: 0,
            status: 'failed',
            message: event.error,
          };
          
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
    }

    const subscriptionId = this.generateSubscriptionId();
    this.imageCallbacks.set(subscriptionId, callback);

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'imageProgress',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Subscribe to spend update events
   */
  onSpendUpdate(
    callback: SpendUpdateCallback,
    options?: NotificationOptions
  ): NotificationSubscription {
    // Ensure task hub client is initialized
    if (!this.taskHubClient) {
      this.taskHubClient = this.signalRService.getTaskHubClient();
      
      // Set up event handlers for spend updates
      // Note: The current task hub doesn't directly support spend update events
      // This would need to be implemented on the server side
      // For now, we'll set up the structure for future implementation
    }

    const subscriptionId = this.generateSubscriptionId();
    this.spendUpdateCallbacks.set(subscriptionId, callback);

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'spendUpdate',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Subscribe to spend limit alert events
   */
  onSpendLimitAlert(
    callback: SpendLimitAlertCallback,
    options?: NotificationOptions
  ): NotificationSubscription {
    // Ensure task hub client is initialized
    if (!this.taskHubClient) {
      this.taskHubClient = this.signalRService.getTaskHubClient();
      
      // Set up event handlers for spend limit alerts
      // Note: The current task hub doesn't directly support spend limit alert events
      // This would need to be implemented on the server side
      // For now, we'll set up the structure for future implementation
    }

    const subscriptionId = this.generateSubscriptionId();
    this.spendLimitCallbacks.set(subscriptionId, callback);

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'spendLimitAlert',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Subscribe to updates for a specific task
   */
  async subscribeToTask(
    taskId: string,
    taskType: 'video' | 'image' | 'batch' | 'other',
    callback: TaskUpdateCallback,
    options?: NotificationOptions
  ): Promise<NotificationSubscription> {
    const subscriptionId = this.generateSubscriptionId();
    
    // Subscribe to the task
    await this.signalRService.subscribeToTask(taskId, taskType);
    
    // Store the callback
    this.taskCallbacks.set(subscriptionId, { taskId, callback });

    // Set up appropriate listeners based on task type
    if (taskType === 'video') {
      if (!this.videoHubClient) {
        this.videoHubClient = this.signalRService.getVideoGenerationHubClient();
      }
      
      // We've already set up the handlers in onVideoProgress
    } else if (taskType === 'image') {
      if (!this.imageHubClient) {
        this.imageHubClient = this.signalRService.getImageGenerationHubClient();
      }
      
      // We've already set up the handlers in onImageProgress
    } else {
      if (!this.taskHubClient) {
        this.taskHubClient = this.signalRService.getTaskHubClient();
      }
      
      // Set up generic task handlers if not already done
      if (!this.taskHubClient.onTaskProgress) {
        this.taskHubClient.onTaskProgress = (event) => {
          // Notify task-specific callbacks
          for (const [subId, taskInfo] of this.taskCallbacks) {
            if (taskInfo.taskId === event.taskId) {
              const subscription = this.subscriptions.get(subId);
              if (!subscription) continue;
              
              const notificationEvent: TaskUpdateEvent = {
                taskId: event.taskId,
                taskType: taskType,
                status: 'processing',
                progress: event.progress,
                metadata: {},
              };
              
              taskInfo.callback(notificationEvent);
            }
          }
          return Promise.resolve();
        };
      }
      
      if (!this.taskHubClient.onTaskCompleted) {
        this.taskHubClient.onTaskCompleted = (event) => {
          // Notify task-specific callbacks
          for (const [subId, taskInfo] of this.taskCallbacks) {
            if (taskInfo.taskId === event.taskId) {
              const subscription = this.subscriptions.get(subId);
              if (!subscription) continue;
              
              const notificationEvent: TaskUpdateEvent = {
                taskId: event.taskId,
                taskType: taskType,
                status: 'completed',
                result: event.result,
              };
              
              taskInfo.callback(notificationEvent);
            }
          }
          return Promise.resolve();
        };
      }
      
      if (!this.taskHubClient.onTaskFailed) {
        this.taskHubClient.onTaskFailed = (event) => {
          // Notify task-specific callbacks
          for (const [subId, taskInfo] of this.taskCallbacks) {
            if (taskInfo.taskId === event.taskId) {
              const subscription = this.subscriptions.get(subId);
              if (!subscription) continue;
              
              const notificationEvent: TaskUpdateEvent = {
                taskId: event.taskId,
                taskType: taskType,
                status: 'failed',
                error: event.error,
              };
              
              taskInfo.callback(notificationEvent);
            }
          }
          return Promise.resolve();
        };
      }
    }

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'taskUpdate',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Unsubscribe from a specific task
   */
  async unsubscribeFromTask(taskId: string): Promise<void> {
    // Find and remove all subscriptions for this task
    const toRemove: string[] = [];
    
    for (const [id, taskInfo] of this.taskCallbacks) {
      if (taskInfo.taskId === taskId) {
        toRemove.push(id);
      }
    }

    toRemove.forEach(id => this.unsubscribe(id));
    
    // Unsubscribe from the task on the server
    await this.signalRService.unsubscribeFromTask(taskId);
  }

  /**
   * Unsubscribe from all notifications
   */
  unsubscribeAll(): void {
    const subscriptionIds = Array.from(this.subscriptions.keys());
    subscriptionIds.forEach(id => this.unsubscribe(id));
    this.connectionStateCallbacks.clear();
  }

  /**
   * Get all active subscriptions
   */
  getActiveSubscriptions(): NotificationSubscription[] {
    return Array.from(this.subscriptions.values());
  }

  /**
   * Connect to SignalR hubs
   */
  async connect(): Promise<void> {
    await this.signalRService.startAllConnections();
  }

  /**
   * Disconnect from SignalR hubs
   */
  async disconnect(): Promise<void> {
    await this.signalRService.stopAllConnections();
  }

  /**
   * Check if connected to SignalR hubs
   */
  isConnected(): boolean {
    const states = this.signalRService.getConnectionStatus();
    return Object.values(states).some(state => state === HubConnectionState.Connected);
  }

  private unsubscribe(subscriptionId: string): void {
    this.subscriptions.delete(subscriptionId);
    this.videoCallbacks.delete(subscriptionId);
    this.imageCallbacks.delete(subscriptionId);
    this.spendUpdateCallbacks.delete(subscriptionId);
    this.spendLimitCallbacks.delete(subscriptionId);
    this.taskCallbacks.delete(subscriptionId);
    
    // If no more subscriptions, we could disconnect, but we'll keep connections alive
    // for better performance in case of new subscriptions
  }

  private generateSubscriptionId(): string {
    return `sub_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }
}
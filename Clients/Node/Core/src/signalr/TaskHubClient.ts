import * as signalR from '@microsoft/signalr';
import { BaseSignalRConnection } from './BaseSignalRConnection';
import { 
  SignalREndpoints,
  ITaskHubServer,
  TaskStartedEvent,
  TaskProgressEvent,
  TaskCompletedEvent,
  TaskFailedEvent,
  TaskCancelledEvent,
  TaskTimedOutEvent
} from '../models/signalr';

/**
 * SignalR client for the Task Hub, providing real-time task progress notifications.
 */
export class TaskHubClient extends BaseSignalRConnection implements ITaskHubServer {
  /**
   * Gets the hub path for task notifications.
   */
  protected get hubPath(): string {
    return SignalREndpoints.TaskHub;
  }

  /**
   * Event handlers for task notifications.
   */
  onTaskStarted?: (event: TaskStartedEvent) => Promise<void>;
  onTaskProgress?: (event: TaskProgressEvent) => Promise<void>;
  onTaskCompleted?: (event: TaskCompletedEvent) => Promise<void>;
  onTaskFailed?: (event: TaskFailedEvent) => Promise<void>;
  onTaskCancelled?: (event: TaskCancelledEvent) => Promise<void>;
  onTaskTimedOut?: (event: TaskTimedOutEvent) => Promise<void>;

  /**
   * Configures the hub-specific event handlers.
   */
  protected configureHubHandlers(connection: signalR.HubConnection): void {
    connection.on('TaskStarted', async (taskId: string, taskType: string, metadata: any) => {
      console.debug(`Task started: ${taskId}, Type: ${taskType}`);
      if (this.onTaskStarted) {
        await this.onTaskStarted({ eventType: 'TaskStarted', taskId, taskType, metadata });
      }
    });

    connection.on('TaskProgress', async (taskId: string, progress: number, message?: string) => {
      console.debug(`Task progress: ${taskId}, Progress: ${progress}%`);
      if (this.onTaskProgress) {
        await this.onTaskProgress({ eventType: 'TaskProgress', taskId, progress, message });
      }
    });

    connection.on('TaskCompleted', async (taskId: string, result: any) => {
      console.debug(`Task completed: ${taskId}`);
      if (this.onTaskCompleted) {
        await this.onTaskCompleted({ eventType: 'TaskCompleted', taskId, result });
      }
    });

    connection.on('TaskFailed', async (taskId: string, error: string, isRetryable: boolean) => {
      console.debug(`Task failed: ${taskId}, Error: ${error}, Retryable: ${isRetryable}`);
      if (this.onTaskFailed) {
        await this.onTaskFailed({ eventType: 'TaskFailed', taskId, error, isRetryable });
      }
    });

    connection.on('TaskCancelled', async (taskId: string, reason?: string) => {
      console.debug(`Task cancelled: ${taskId}, Reason: ${reason}`);
      if (this.onTaskCancelled) {
        await this.onTaskCancelled({ eventType: 'TaskCancelled', taskId, reason });
      }
    });

    connection.on('TaskTimedOut', async (taskId: string, timeoutSeconds: number) => {
      console.debug(`Task timed out: ${taskId}, Timeout: ${timeoutSeconds}s`);
      if (this.onTaskTimedOut) {
        await this.onTaskTimedOut({ eventType: 'TaskTimedOut', taskId, timeoutSeconds });
      }
    });
  }

  /**
   * Subscribe to notifications for a specific task.
   */
  async subscribeToTask(taskId: string): Promise<void> {
    if (!taskId?.trim()) {
      throw new Error('Task ID cannot be null or empty');
    }

    await this.invoke('SubscribeToTask', taskId);
    console.debug(`Subscribed to task: ${taskId}`);
  }

  /**
   * Unsubscribe from notifications for a specific task.
   */
  async unsubscribeFromTask(taskId: string): Promise<void> {
    if (!taskId?.trim()) {
      throw new Error('Task ID cannot be null or empty');
    }

    await this.invoke('UnsubscribeFromTask', taskId);
    console.debug(`Unsubscribed from task: ${taskId}`);
  }

  /**
   * Subscribe to notifications for all tasks of a specific type.
   */
  async subscribeToTaskType(taskType: string): Promise<void> {
    if (!taskType?.trim()) {
      throw new Error('Task type cannot be null or empty');
    }

    await this.invoke('SubscribeToTaskType', taskType);
    console.debug(`Subscribed to task type: ${taskType}`);
  }

  /**
   * Unsubscribe from notifications for a task type.
   */
  async unsubscribeFromTaskType(taskType: string): Promise<void> {
    if (!taskType?.trim()) {
      throw new Error('Task type cannot be null or empty');
    }

    await this.invoke('UnsubscribeFromTaskType', taskType);
    console.debug(`Unsubscribed from task type: ${taskType}`);
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

  /**
   * Subscribe to multiple task types at once.
   */
  async subscribeToTaskTypes(taskTypes: string[]): Promise<void> {
    await Promise.all(taskTypes.map(taskType => this.subscribeToTaskType(taskType)));
  }

  /**
   * Unsubscribe from multiple task types at once.
   */
  async unsubscribeFromTaskTypes(taskTypes: string[]): Promise<void> {
    await Promise.all(taskTypes.map(taskType => this.unsubscribeFromTaskType(taskType)));
  }
}
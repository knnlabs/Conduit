import { useCallback, useEffect, useRef } from 'react';
import { TaskHubClient } from '../../../signalr/TaskHubClient';
import { 
  TaskStartedEvent, 
  TaskProgressEvent, 
  TaskCompletedEvent, 
  TaskFailedEvent,
  TaskCancelledEvent,
  TaskTimedOutEvent,
  TaskEvent
} from '../../../models/signalr';
import { useSignalRConnection, UseSignalRConnectionOptions } from './useSignalRConnection';

export interface TaskEventHandlers {
  onTaskStarted?: (event: TaskStartedEvent) => void;
  onTaskProgress?: (event: TaskProgressEvent) => void;
  onTaskCompleted?: (event: TaskCompletedEvent) => void;
  onTaskFailed?: (event: TaskFailedEvent) => void;
  onTaskCancelled?: (event: TaskCancelledEvent) => void;
  onTaskTimedOut?: (event: TaskTimedOutEvent) => void;
  onAnyTaskEvent?: (event: TaskEvent) => void;
}

export interface UseTaskHubOptions extends UseSignalRConnectionOptions, TaskEventHandlers {
  taskId?: string;
  taskType?: string;
  autoSubscribe?: boolean;
}

export interface UseTaskHubReturn {
  isConnected: boolean;
  isConnecting: boolean;
  isReconnecting: boolean;
  error?: Error;
  subscribeToTask: (taskId: string) => Promise<void>;
  unsubscribeFromTask: (taskId: string) => Promise<void>;
  subscribeToTaskType: (taskType: string) => Promise<void>;
  unsubscribeFromTaskType: (taskType: string) => Promise<void>;
}

export function useTaskHub(options: UseTaskHubOptions = {}): UseTaskHubReturn {
  const {
    taskId,
    taskType,
    autoSubscribe = true,
    onTaskStarted,
    onTaskProgress,
    onTaskCompleted,
    onTaskFailed,
    onTaskCancelled,
    onTaskTimedOut,
    onAnyTaskEvent,
    ...connectionOptions
  } = options;

  const handlersRef = useRef<TaskEventHandlers>({
    onTaskStarted,
    onTaskProgress,
    onTaskCompleted,
    onTaskFailed,
    onTaskCancelled,
    onTaskTimedOut,
    onAnyTaskEvent,
  });

  // Update handlers ref when they change
  useEffect(() => {
    handlersRef.current = {
      onTaskStarted,
      onTaskProgress,
      onTaskCompleted,
      onTaskFailed,
      onTaskCancelled,
      onTaskTimedOut,
      onAnyTaskEvent,
    };
  }, [onTaskStarted, onTaskProgress, onTaskCompleted, onTaskFailed, onTaskCancelled, onTaskTimedOut, onAnyTaskEvent]);

  const createTaskHubClient = useCallback((baseUrl: string, apiKey: string) => {
    return new TaskHubClient(baseUrl, apiKey);
  }, []);

  const { connection, state } = useSignalRConnection(
    createTaskHubClient,
    '/hubs/tasks',
    connectionOptions
  );

  // Set up event handlers when connection is established
  useEffect(() => {
    if (!connection) return;

    // Assign event handlers
    connection.onTaskStarted = async (event: TaskStartedEvent) => {
      handlersRef.current.onTaskStarted?.(event);
      handlersRef.current.onAnyTaskEvent?.(event);
    };

    connection.onTaskProgress = async (event: TaskProgressEvent) => {
      handlersRef.current.onTaskProgress?.(event);
      handlersRef.current.onAnyTaskEvent?.(event);
    };

    connection.onTaskCompleted = async (event: TaskCompletedEvent) => {
      handlersRef.current.onTaskCompleted?.(event);
      handlersRef.current.onAnyTaskEvent?.(event);
    };

    connection.onTaskFailed = async (event: TaskFailedEvent) => {
      handlersRef.current.onTaskFailed?.(event);
      handlersRef.current.onAnyTaskEvent?.(event);
    };

    connection.onTaskCancelled = async (event: TaskCancelledEvent) => {
      handlersRef.current.onTaskCancelled?.(event);
      handlersRef.current.onAnyTaskEvent?.(event);
    };

    connection.onTaskTimedOut = async (event: TaskTimedOutEvent) => {
      handlersRef.current.onTaskTimedOut?.(event);
      handlersRef.current.onAnyTaskEvent?.(event);
    };

    // Clean up function to remove handlers
    return () => {
      connection.onTaskStarted = undefined;
      connection.onTaskProgress = undefined;
      connection.onTaskCompleted = undefined;
      connection.onTaskFailed = undefined;
      connection.onTaskCancelled = undefined;
      connection.onTaskTimedOut = undefined;
    };
  }, [connection]);

  // Auto-subscribe to task or task type
  useEffect(() => {
    if (!connection || !state.isConnected || !autoSubscribe) return;

    const subscribe = async () => {
      try {
        if (taskId) {
          await connection.subscribeToTask(taskId);
        }
        if (taskType) {
          await connection.subscribeToTaskType(taskType);
        }
      } catch (error) {
        console.error('Failed to subscribe:', error);
      }
    };

    subscribe();

    // Cleanup function to unsubscribe
    return () => {
      if (taskId) {
        connection.unsubscribeFromTask(taskId).catch(console.error);
      }
      if (taskType) {
        connection.unsubscribeFromTaskType(taskType).catch(console.error);
      }
    };
  }, [connection, state.isConnected, taskId, taskType, autoSubscribe]);

  const subscribeToTask = useCallback(async (taskId: string) => {
    if (!connection) {
      throw new Error('Not connected to task hub');
    }
    await connection.subscribeToTask(taskId);
  }, [connection]);

  const unsubscribeFromTask = useCallback(async (taskId: string) => {
    if (!connection) {
      throw new Error('Not connected to task hub');
    }
    await connection.unsubscribeFromTask(taskId);
  }, [connection]);

  const subscribeToTaskType = useCallback(async (taskType: string) => {
    if (!connection) {
      throw new Error('Not connected to task hub');
    }
    await connection.subscribeToTaskType(taskType);
  }, [connection]);

  const unsubscribeFromTaskType = useCallback(async (taskType: string) => {
    if (!connection) {
      throw new Error('Not connected to task hub');
    }
    await connection.unsubscribeFromTaskType(taskType);
  }, [connection]);

  return {
    isConnected: state.isConnected,
    isConnecting: state.isConnecting,
    isReconnecting: state.isReconnecting,
    error: state.error,
    subscribeToTask,
    unsubscribeFromTask,
    subscribeToTaskType,
    unsubscribeFromTaskType,
  };
}
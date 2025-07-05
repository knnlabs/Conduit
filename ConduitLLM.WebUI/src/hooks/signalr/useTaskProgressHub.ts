'use client';

import { useEffect, useState, useCallback } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { notifications } from '@mantine/notifications';
import { safeLog } from '@/lib/utils/logging';

export interface TaskProgress {
  taskId: string;
  type: 'image' | 'video' | 'audio_transcription' | 'audio_speech';
  status: 'pending' | 'processing' | 'completed' | 'failed';
  progress: number;
  message?: string;
  result?: unknown;
  error?: string;
  startedAt: Date;
  completedAt?: Date;
}

export interface TaskProgressCallbacks {
  onTaskStarted?: (task: TaskProgress) => void;
  onTaskProgress?: (task: TaskProgress) => void;
  onTaskCompleted?: (task: TaskProgress) => void;
  onTaskFailed?: (task: TaskProgress) => void;
}

export function useTaskProgressHub(
  callbacks?: TaskProgressCallbacks
) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [tasks, setTasks] = useState<Map<string, TaskProgress>>(new Map());

  const updateTask = useCallback((task: TaskProgress) => {
    setTasks(prev => new Map(prev.set(task.taskId, task)));
    
    // Trigger appropriate callback
    switch (task.status) {
      case 'pending':
        callbacks?.onTaskStarted?.(task);
        break;
      case 'processing':
        callbacks?.onTaskProgress?.(task);
        break;
      case 'completed':
        callbacks?.onTaskCompleted?.(task);
        notifications.show({
          title: 'Task Completed',
          message: `${task.type} generation completed successfully`,
          color: 'green',
        });
        break;
      case 'failed':
        callbacks?.onTaskFailed?.(task);
        notifications.show({
          title: 'Task Failed',
          message: task.error || `${task.type} generation failed`,
          color: 'red',
        });
        break;
    }
  }, [callbacks]);

  useEffect(() => {
    const setupConnection = async () => {
      try {
        // Get SignalR configuration from server
        const configResponse = await fetch('/api/signalr/config');
        if (!configResponse.ok) {
          throw new Error('Failed to get SignalR configuration');
        }
        const { coreUrl } = await configResponse.json();

        // Get session token for authentication
        const getSessionToken = async (): Promise<string> => {
          try {
            const response = await fetch('/api/auth/session-token', {
              method: 'GET',
              credentials: 'include',
            });
            if (response.ok) {
              const data = await response.json();
              return data.token || '';
            }
          } catch (error) {
            console.warn('Failed to get session token for SignalR:', error);
          }
          return '';
        };

        const newConnection = new HubConnectionBuilder()
          .withUrl(`${coreUrl}/hubs/task-progress`, {
            accessTokenFactory: getSessionToken,
          })
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Information)
          .build();

    // Set up event handlers
    newConnection.on('TaskStarted', (task: TaskProgress) => {
      safeLog('Task started', { taskId: task.taskId, type: task.type });
      updateTask({ ...task, startedAt: new Date(task.startedAt) });
    });

    newConnection.on('TaskProgress', (task: TaskProgress) => {
      safeLog('Task progress', { taskId: task.taskId, progress: task.progress });
      updateTask({ 
        ...task, 
        startedAt: new Date(task.startedAt),
        completedAt: task.completedAt ? new Date(task.completedAt) : undefined
      });
    });

    newConnection.on('TaskCompleted', (task: TaskProgress) => {
      safeLog('Task completed', { taskId: task.taskId, type: task.type });
      updateTask({ 
        ...task, 
        startedAt: new Date(task.startedAt),
        completedAt: new Date(task.completedAt!)
      });
    });

    newConnection.on('TaskFailed', (task: TaskProgress) => {
      safeLog('Task failed', { taskId: task.taskId, error: task.error });
      updateTask({ 
        ...task, 
        startedAt: new Date(task.startedAt),
        completedAt: task.completedAt ? new Date(task.completedAt) : undefined
      });
    });

    // Handle connection events
    newConnection.onclose(() => {
      setIsConnected(false);
      safeLog('Task progress hub disconnected');
    });

    newConnection.onreconnecting(() => {
      safeLog('Task progress hub reconnecting...');
    });

    newConnection.onreconnected(() => {
      setIsConnected(true);
      safeLog('Task progress hub reconnected');
    });

        // Start connection
        newConnection.start()
          .then(() => {
            setIsConnected(true);
            setConnection(newConnection);
            safeLog('Task progress hub connected');
          })
          .catch((error) => {
            safeLog('Failed to connect to task progress hub', { error: error.message });
          });

        return () => {
          newConnection.stop();
        };
      } catch (error) {
        console.error('Failed to setup task progress hub connection:', error);
      }
    };

    setupConnection();
  }, [updateTask]);

  const getTask = useCallback((taskId: string): TaskProgress | undefined => {
    return tasks.get(taskId);
  }, [tasks]);

  const getTasksByType = useCallback((type: TaskProgress['type']): TaskProgress[] => {
    return Array.from(tasks.values()).filter(task => task.type === type);
  }, [tasks]);

  const clearTask = useCallback((taskId: string) => {
    setTasks(prev => {
      const newTasks = new Map(prev);
      newTasks.delete(taskId);
      return newTasks;
    });
  }, []);

  const clearCompletedTasks = useCallback(() => {
    setTasks(prev => {
      const newTasks = new Map();
      for (const [taskId, task] of prev) {
        if (task.status === 'processing' || task.status === 'pending') {
          newTasks.set(taskId, task);
        }
      }
      return newTasks;
    });
  }, []);

  return {
    connection,
    isConnected,
    tasks: Array.from(tasks.values()),
    getTask,
    getTasksByType,
    clearTask,
    clearCompletedTasks,
  };
}
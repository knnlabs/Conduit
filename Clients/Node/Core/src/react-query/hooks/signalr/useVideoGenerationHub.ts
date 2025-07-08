import { useCallback, useEffect, useRef } from 'react';
import { VideoGenerationHubClient } from '../../../signalr/VideoGenerationHubClient';
import { 
  VideoGenerationStartedEvent,
  VideoGenerationProgressEvent,
  VideoGenerationCompletedEvent,
  VideoGenerationFailedEvent,
  VideoGenerationEvent
} from '../../../models/signalr';
import { useSignalRConnection, UseSignalRConnectionOptions } from './useSignalRConnection';

export interface VideoGenerationEventHandlers {
  onVideoGenerationStarted?: (event: VideoGenerationStartedEvent) => void;
  onVideoGenerationProgress?: (event: VideoGenerationProgressEvent) => void;
  onVideoGenerationCompleted?: (event: VideoGenerationCompletedEvent) => void;
  onVideoGenerationFailed?: (event: VideoGenerationFailedEvent) => void;
  onAnyVideoEvent?: (event: VideoGenerationEvent) => void;
}

export interface UseVideoGenerationHubOptions extends UseSignalRConnectionOptions, VideoGenerationEventHandlers {
  taskId?: string;
  autoSubscribe?: boolean;
}

export interface UseVideoGenerationHubReturn {
  isConnected: boolean;
  isConnecting: boolean;
  isReconnecting: boolean;
  error?: Error;
  subscribeToTask: (taskId: string) => Promise<void>;
  unsubscribeFromTask: (taskId: string) => Promise<void>;
}

export function useVideoGenerationHub(options: UseVideoGenerationHubOptions = {}): UseVideoGenerationHubReturn {
  const {
    taskId,
    autoSubscribe = true,
    onVideoGenerationStarted,
    onVideoGenerationProgress,
    onVideoGenerationCompleted,
    onVideoGenerationFailed,
    onAnyVideoEvent,
    ...connectionOptions
  } = options;

  const handlersRef = useRef<VideoGenerationEventHandlers>({
    onVideoGenerationStarted,
    onVideoGenerationProgress,
    onVideoGenerationCompleted,
    onVideoGenerationFailed,
    onAnyVideoEvent,
  });

  // Update handlers ref when they change
  useEffect(() => {
    handlersRef.current = {
      onVideoGenerationStarted,
      onVideoGenerationProgress,
      onVideoGenerationCompleted,
      onVideoGenerationFailed,
      onAnyVideoEvent,
    };
  }, [onVideoGenerationStarted, onVideoGenerationProgress, onVideoGenerationCompleted, onVideoGenerationFailed, onAnyVideoEvent]);

  const createVideoGenerationHubClient = useCallback((baseUrl: string, apiKey: string) => {
    return new VideoGenerationHubClient(baseUrl, apiKey);
  }, []);

  const { connection, state } = useSignalRConnection(
    createVideoGenerationHubClient,
    '/hubs/video-generation',
    connectionOptions
  );

  // Set up event handlers when connection is established
  useEffect(() => {
    if (!connection) return;

    // Assign event handlers
    connection.onVideoGenerationStarted = async (event: VideoGenerationStartedEvent) => {
      handlersRef.current.onVideoGenerationStarted?.(event);
      handlersRef.current.onAnyVideoEvent?.(event);
    };

    connection.onVideoGenerationProgress = async (event: VideoGenerationProgressEvent) => {
      handlersRef.current.onVideoGenerationProgress?.(event);
      handlersRef.current.onAnyVideoEvent?.(event);
    };

    connection.onVideoGenerationCompleted = async (event: VideoGenerationCompletedEvent) => {
      handlersRef.current.onVideoGenerationCompleted?.(event);
      handlersRef.current.onAnyVideoEvent?.(event);
    };

    connection.onVideoGenerationFailed = async (event: VideoGenerationFailedEvent) => {
      handlersRef.current.onVideoGenerationFailed?.(event);
      handlersRef.current.onAnyVideoEvent?.(event);
    };

    // Clean up function to remove handlers
    return () => {
      connection.onVideoGenerationStarted = undefined;
      connection.onVideoGenerationProgress = undefined;
      connection.onVideoGenerationCompleted = undefined;
      connection.onVideoGenerationFailed = undefined;
    };
  }, [connection]);

  // Auto-subscribe to task
  useEffect(() => {
    if (!connection || !state.isConnected || !autoSubscribe || !taskId) return;

    const subscribe = async () => {
      try {
        await connection.subscribeToTask(taskId);
      } catch (error) {
        console.error('Failed to subscribe to video generation task:', error);
      }
    };

    subscribe();

    // Cleanup function to unsubscribe
    return () => {
      connection.unsubscribeFromTask(taskId).catch(console.error);
    };
  }, [connection, state.isConnected, taskId, autoSubscribe]);

  const subscribeToTask = useCallback(async (taskId: string) => {
    if (!connection) {
      throw new Error('Not connected to video generation hub');
    }
    await connection.subscribeToTask(taskId);
  }, [connection]);

  const unsubscribeFromTask = useCallback(async (taskId: string) => {
    if (!connection) {
      throw new Error('Not connected to video generation hub');
    }
    await connection.unsubscribeFromTask(taskId);
  }, [connection]);

  return {
    isConnected: state.isConnected,
    isConnecting: state.isConnecting,
    isReconnecting: state.isReconnecting,
    error: state.error,
    subscribeToTask,
    unsubscribeFromTask,
  };
}
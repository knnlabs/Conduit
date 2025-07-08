import { useCallback, useEffect, useRef } from 'react';
import { ImageGenerationHubClient } from '../../../signalr/ImageGenerationHubClient';
import { 
  ImageGenerationStartedEvent,
  ImageGenerationProgressEvent,
  ImageGenerationCompletedEvent,
  ImageGenerationFailedEvent,
  ImageGenerationEvent
} from '../../../models/signalr';
import { useSignalRConnection, UseSignalRConnectionOptions } from './useSignalRConnection';

export interface ImageGenerationEventHandlers {
  onImageGenerationStarted?: (event: ImageGenerationStartedEvent) => void;
  onImageGenerationProgress?: (event: ImageGenerationProgressEvent) => void;
  onImageGenerationCompleted?: (event: ImageGenerationCompletedEvent) => void;
  onImageGenerationFailed?: (event: ImageGenerationFailedEvent) => void;
  onAnyImageEvent?: (event: ImageGenerationEvent) => void;
}

export interface UseImageGenerationHubOptions extends UseSignalRConnectionOptions, ImageGenerationEventHandlers {
  taskId?: string;
  autoSubscribe?: boolean;
}

export interface UseImageGenerationHubReturn {
  isConnected: boolean;
  isConnecting: boolean;
  isReconnecting: boolean;
  error?: Error;
  subscribeToTask: (taskId: string) => Promise<void>;
  unsubscribeFromTask: (taskId: string) => Promise<void>;
}

export function useImageGenerationHub(options: UseImageGenerationHubOptions = {}): UseImageGenerationHubReturn {
  const {
    taskId,
    autoSubscribe = true,
    onImageGenerationStarted,
    onImageGenerationProgress,
    onImageGenerationCompleted,
    onImageGenerationFailed,
    onAnyImageEvent,
    ...connectionOptions
  } = options;

  const handlersRef = useRef<ImageGenerationEventHandlers>({
    onImageGenerationStarted,
    onImageGenerationProgress,
    onImageGenerationCompleted,
    onImageGenerationFailed,
    onAnyImageEvent,
  });

  // Update handlers ref when they change
  useEffect(() => {
    handlersRef.current = {
      onImageGenerationStarted,
      onImageGenerationProgress,
      onImageGenerationCompleted,
      onImageGenerationFailed,
      onAnyImageEvent,
    };
  }, [onImageGenerationStarted, onImageGenerationProgress, onImageGenerationCompleted, onImageGenerationFailed, onAnyImageEvent]);

  const createImageGenerationHubClient = useCallback((baseUrl: string, apiKey: string) => {
    return new ImageGenerationHubClient(baseUrl, apiKey);
  }, []);

  const { connection, state } = useSignalRConnection(
    createImageGenerationHubClient,
    '/hubs/image-generation',
    connectionOptions
  );

  // Set up event handlers when connection is established
  useEffect(() => {
    if (!connection) return;

    // Assign event handlers
    connection.onImageGenerationStarted = async (event: ImageGenerationStartedEvent) => {
      handlersRef.current.onImageGenerationStarted?.(event);
      handlersRef.current.onAnyImageEvent?.(event);
    };

    connection.onImageGenerationProgress = async (event: ImageGenerationProgressEvent) => {
      handlersRef.current.onImageGenerationProgress?.(event);
      handlersRef.current.onAnyImageEvent?.(event);
    };

    connection.onImageGenerationCompleted = async (event: ImageGenerationCompletedEvent) => {
      handlersRef.current.onImageGenerationCompleted?.(event);
      handlersRef.current.onAnyImageEvent?.(event);
    };

    connection.onImageGenerationFailed = async (event: ImageGenerationFailedEvent) => {
      handlersRef.current.onImageGenerationFailed?.(event);
      handlersRef.current.onAnyImageEvent?.(event);
    };

    // Clean up function to remove handlers
    return () => {
      connection.onImageGenerationStarted = undefined;
      connection.onImageGenerationProgress = undefined;
      connection.onImageGenerationCompleted = undefined;
      connection.onImageGenerationFailed = undefined;
    };
  }, [connection]);

  // Auto-subscribe to task
  useEffect(() => {
    if (!connection || !state.isConnected || !autoSubscribe || !taskId) return;

    const subscribe = async () => {
      try {
        await connection.subscribeToTask(taskId);
      } catch (error) {
        console.error('Failed to subscribe to image generation task:', error);
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
      throw new Error('Not connected to image generation hub');
    }
    await connection.subscribeToTask(taskId);
  }, [connection]);

  const unsubscribeFromTask = useCallback(async (taskId: string) => {
    if (!connection) {
      throw new Error('Not connected to image generation hub');
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
import { useState, useCallback, useRef, useEffect } from 'react';
import { useVideoStore } from './useVideoStore';
import { videoSignalRClient } from '@/lib/client/videoSignalRClient';
import { getBrowserCoreClient } from '@/lib/client/browserCoreClient';
import type { 
  VideoSettings, 
  VideoTask, 
  VideoGenerationResult
} from '../types';
import { 
  createToastErrorHandler, 
  shouldShowBalanceWarning,
  type VideoProgressCallbacks
} from '@knn_labs/conduit-core-client';
import { notifications } from '@mantine/notifications';

interface GenerateVideoParams {
  prompt: string;
  settings: VideoSettings;
  dynamicParameters?: Record<string, unknown>;
}

interface UseEnhancedVideoGenerationOptions {
  /** Fallback to polling if SignalR fails */
  fallbackToPolling?: boolean;
}

/**
 * Enhanced video generation hook that uses the new SDK progress tracking
 * Falls back to polling-based approach if SignalR is not available
 */
export function useEnhancedVideoGeneration(options: UseEnhancedVideoGenerationOptions = {}) {
  const {
    // fallbackToPolling is reserved for future use
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    fallbackToPolling = true,
  } = options;

  const [isGenerating, setIsGenerating] = useState(false);
  const [isRetrying] = useState(false);
  const [signalRConnected] = useState(false);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const { addTask, updateTask, setError } = useVideoStore();

  // Track SignalR connection errors
  const signalRErrorCount = useRef(0);
  const maxSignalRErrors = 3;
  
  // Create error handler with toast notifications
  const handleError = createToastErrorHandler(notifications.show);

  useEffect(() => {
    // Store ref in closure to avoid stale closure warning
    const intervalRef = pollingIntervalRef;
    
    // Cleanup on unmount
    return () => {
      const intervalId = intervalRef.current;
      if (intervalId) {
        clearInterval(intervalId);
      }
      void videoSignalRClient.disconnect();
    };
  }, []);

  const generateVideo = useCallback(async ({ prompt, settings, dynamicParameters }: GenerateVideoParams) => {
    setIsGenerating(true);
    setError(null);

    try {
      // Get the SDK client with ephemeral key
      const client = await getBrowserCoreClient();
      
      // Prepare the video generation request
      const request = {
        prompt,
        model: settings.model,
        duration: settings.duration,
        size: settings.size,
        fps: settings.fps,
        style: settings.style,
        response_format: settings.responseFormat,
        // Include dynamic parameters from the UI (e.g., start_image for Kling)
        ...dynamicParameters,
      };

      // Track the task ID for use in callbacks
      let currentTaskId = '';
      
      // Define progress callbacks for the SDK
      const progressCallbacks: VideoProgressCallbacks = {
        onStarted: (taskId, estimatedSeconds) => {
          console.warn(`Video generation started: ${taskId}, estimated time: ${estimatedSeconds}s`);
          currentTaskId = taskId;
          
          const task: VideoTask = {
            id: taskId,
            prompt,
            status: 'pending',
            progress: 0,
            estimatedTimeToCompletion: estimatedSeconds,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            settings,
            retryCount: 0,
            retryHistory: [],
          };
          addTask(task);
        },
        onProgress: (progress) => {
          console.warn(`Video generation progress: ${progress.percentage}%`);
          // Map SDK status to VideoTask status
          let taskStatus: VideoTask['status'] = 'running';
          if (progress.status === 'completed') taskStatus = 'completed';
          else if (progress.status === 'failed') taskStatus = 'failed';
          else if (progress.status === 'cancelled') taskStatus = 'cancelled';
          
          updateTask(currentTaskId, {
            progress: progress.percentage,
            status: taskStatus,
            message: progress.message,
            updatedAt: new Date().toISOString(),
          });
        },
        onCompleted: (result) => {
          console.warn('Video generation completed:', result);
          
          // The SDK returns VideoGenerationResponse, convert to local VideoGenerationResult
          updateTask(currentTaskId, {
            status: 'completed',
            progress: 100,
            result: result as VideoGenerationResult,
            updatedAt: new Date().toISOString(),
          });
          
          // Show success notification
          notifications.show({
            title: 'Video Generated',
            message: 'Your video has been generated successfully!',
            color: 'green',
          });
        },
        onFailed: (error) => {
          console.error('Video generation failed:', error);
          
          const errorMessage = typeof error === 'string' ? error : 'Video generation failed';
          setError(errorMessage);
          
          // Update task status
          updateTask(currentTaskId, {
            status: 'failed',
            error: errorMessage,
            updatedAt: new Date().toISOString(),
          });
          
          notifications.show({
            title: 'Video Generation Failed',
            message: errorMessage,
            color: 'red',
          });
        },
      };

      // Use SDK's generateWithProgress method which handles SignalR + polling
      const { taskId, result } = await client.videos.generateWithProgress(
        request,
        progressCallbacks
      );

      console.warn('Video generation initiated with task ID:', taskId);
      
      // Wait for the result (this promise is already being tracked by callbacks)
      await result;
      
    } catch (error) {
      // Use enhanced error handler with toast notifications
      const errorMessage = handleError(error, 'generate video');
      setError(errorMessage);
      setIsGenerating(false);
      
      // Special handling for balance errors
      if (shouldShowBalanceWarning(error)) {
        setError('Please add credits to your account to generate videos.');
      }
    } finally {
      setIsGenerating(false);
    }
  }, [
    addTask,
    updateTask,
    setError,
    handleError
  ]);

  const cancelGeneration = useCallback(async (taskId: string) => {
    try {
      // Get the SDK client with ephemeral key
      const client = await getBrowserCoreClient();
      
      // Use SDK to cancel the task
      await client.videos.cancelTask(taskId);

      updateTask(taskId, { status: 'cancelled' });
      
      // Disconnect SignalR if connected
      await videoSignalRClient.disconnect();
      
      setIsGenerating(false);
    } catch (error) {
      console.error('Error cancelling task:', error);
      setError(error instanceof Error ? error.message : 'Failed to cancel task');
    }
  }, [updateTask, setError]);

  const retryGeneration = useCallback(async (task: VideoTask) => {
    try {
      // Add to retry history
      const retryHistoryEntry = {
        attemptNumber: task.retryCount + 1,
        timestamp: new Date().toISOString(),
        error: task.error ?? 'Unknown error',
      };

      // Update task with retry status
      updateTask(task.id, {
        status: 'pending',
        retryCount: task.retryCount + 1,
        lastRetryAt: new Date().toISOString(),
        retryHistory: [...task.retryHistory, retryHistoryEntry],
        error: undefined,
        progress: 0,
        message: `Retrying (attempt ${task.retryCount + 2})...`,
        updatedAt: new Date().toISOString(),
      });

      // Retry the generation with the same settings
      await generateVideo({
        prompt: task.prompt,
        settings: task.settings,
      });
    } catch (error) {
      console.error('Error retrying video generation:', error);
      const errorMessage = error instanceof Error ? error.message : 'Failed to retry generation';
      
      updateTask(task.id, {
        status: 'failed',
        error: errorMessage,
        message: 'Retry failed',
        updatedAt: new Date().toISOString(),
      });
      
      setError(errorMessage);
    }
  }, [generateVideo, updateTask, setError]);

  return {
    generateVideo,
    cancelGeneration,
    retryGeneration,
    isGenerating,
    isRetrying,
    signalRConnected,
    isProgressTrackingEnabled: signalRErrorCount.current < maxSignalRErrors,
  };
}
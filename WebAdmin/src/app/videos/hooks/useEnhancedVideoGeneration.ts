import { useState, useCallback } from 'react';
import { useVideoStore } from './useVideoStore';
import { disconnectVideoSignalRClient } from '@/lib/client/videoSignalRClient';
import { getBrowserCoreClient } from '@/lib/client/browserCoreClient';
import type { 
  VideoSettings, 
  VideoTask, 
  VideoGenerationResult
} from '../types';
import { MediaGenerationStatus, mapLegacyStatus } from '@/app/types/media';
import { 
  createToastErrorHandler, 
  shouldShowBalanceWarning,
  type VideoProgressCallbacks
} from '@/lib/gateway-api';
import { notify } from '@/lib/notifications';
import { notifications } from '@mantine/notifications'; // Required by createToastErrorHandler SDK callback

interface GenerateVideoParams {
  prompt: string;
  settings: VideoSettings;
  dynamicParameters?: Record<string, unknown>;
}

/**
 * Video generation hook that delegates progress transport, including SignalR and polling
 * fallback behavior, to the SDK's generateWithProgress implementation.
 */
export function useEnhancedVideoGeneration() {
  const [isGenerating, setIsGenerating] = useState(false);
  const { addTask, updateTask, setError } = useVideoStore();
  
  // Create error handler with toast notifications
  const handleError = createToastErrorHandler(notifications.show);

  const generateVideo = useCallback(async ({ prompt, settings, dynamicParameters }: GenerateVideoParams) => {
    setIsGenerating(true);
    setError(null);

    try {
      // Get the SDK client with ephemeral key
      const client = await getBrowserCoreClient();
      
      // Prepare the video generation request - only model and dynamic parameters
      const request = {
        prompt,
        model: settings.model,
        // Include all dynamic parameters from the UI (duration, size, fps, style, etc.)
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
            status: MediaGenerationStatus.Pending,
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
          // Map SDK status to MediaGenerationStatus
          const taskStatus = mapLegacyStatus(progress.status);
          
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
            status: MediaGenerationStatus.Completed,
            progress: 100,
            result: result as VideoGenerationResult,
            updatedAt: new Date().toISOString(),
          });
          
          // Show success notification
          notify.success('Your video has been generated successfully!', 'Video Generated');
        },
        onFailed: (error) => {
          console.error('Video generation failed:', error);

          // Use SDK error handler for consistent error extraction and toast display
          const errorMessage = handleError(error, 'video generation');
          setError(errorMessage);

          // Update task status
          updateTask(currentTaskId, {
            status: MediaGenerationStatus.Failed,
            error: errorMessage,
            updatedAt: new Date().toISOString(),
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

      updateTask(taskId, { status: MediaGenerationStatus.Cancelled });
      
      // Disconnect only this task's SignalR connection.
      await disconnectVideoSignalRClient(taskId);
      
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
        status: MediaGenerationStatus.Pending,
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
        status: MediaGenerationStatus.Failed,
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
  };
}

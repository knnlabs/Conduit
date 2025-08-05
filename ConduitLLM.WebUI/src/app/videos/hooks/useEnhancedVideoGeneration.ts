import { useState, useCallback, useRef, useEffect } from 'react';
import { useVideoStore } from './useVideoStore';
import { generateVideoWithProgress, cleanupClientCore } from '@/lib/client/coreClient';
import type { 
  VideoSettings, 
  VideoTask, 
  VideoGenerationResult, 
  AsyncVideoGenerationResponse
} from '../types';
import { 
  createToastErrorHandler, 
  shouldShowBalanceWarning,
  handleApiError
} from '@knn_labs/conduit-core-client';

// Define VideoProgress type locally since SDK is broken
interface VideoProgress {
  percentage?: number;
  status?: string;
  message?: string;
}
import { notifications } from '@mantine/notifications';

interface GenerateVideoParams {
  prompt: string;
  settings: VideoSettings;
}

interface UseEnhancedVideoGenerationOptions {
  /** Whether to use the new progress tracking method */
  useProgressTracking?: boolean;
  /** Fallback to polling if SignalR fails */
  fallbackToPolling?: boolean;
}

/**
 * Enhanced video generation hook that uses the new SDK progress tracking
 * Falls back to polling-based approach if SignalR is not available
 */
export function useEnhancedVideoGeneration(options: UseEnhancedVideoGenerationOptions = {}) {
  const {
    useProgressTracking = true,
    fallbackToPolling = true,
  } = options;

  const [isGenerating, setIsGenerating] = useState(false);
  const [isRetrying] = useState(false);
  const [signalRConnected, setSignalRConnected] = useState(false);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const { addTask, updateTask, setError } = useVideoStore();

  // Track SignalR connection errors
  const signalRErrorCount = useRef(0);
  const maxSignalRErrors = 3;
  
  // Create error handler with toast notifications
  const handleError = createToastErrorHandler(notifications.show);

  useEffect(() => {
    // Cleanup on unmount
    return () => {
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
      }
      cleanupClientCore();
    };
  }, []);

  const generateVideo = useCallback(async ({ prompt, settings }: GenerateVideoParams) => {
    setIsGenerating(true);
    setError(null);

    try {
      // Check if we should use progress tracking
      const shouldUseProgressTracking = useProgressTracking && signalRErrorCount.current < maxSignalRErrors;

      if (shouldUseProgressTracking) {
        // Try to use the new progress tracking method
        try {
          // Create new task in store
          const tempTaskId = `temp_${Date.now()}`;
          const newTask: VideoTask = {
            id: tempTaskId,
            prompt,
            status: 'pending',
            progress: 0,
            message: 'Initializing video generation...',
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            settings,
            retryCount: 0,
            retryHistory: [],
          };
          addTask(newTask);

          // Generate with progress tracking
          const { taskId } = await generateVideoWithProgress(
            {
              prompt,
              model: settings.model,
              duration: settings.duration,
              size: settings.size,
              fps: settings.fps,
              style: settings.style,
            },
            {
              onProgress: (progress: VideoProgress) => {
                updateTask(taskId, {
                  progress: progress.percentage ?? 0,
                  status: progress.status === 'Processing' ? 'running' : (progress.status?.toLowerCase() ?? 'pending') as VideoTask['status'],
                  message: progress.message,
                  updatedAt: new Date().toISOString(),
                });
              },
              onStarted: (actualTaskId: string, estimatedSeconds?: number) => {
                // Update the temporary task ID with the actual one
                updateTask(tempTaskId, {
                  id: actualTaskId,
                  status: 'running',
                  estimatedTimeToCompletion: estimatedSeconds,
                  message: estimatedSeconds ? `Started. Estimated time: ${estimatedSeconds}s` : 'Video generation started',
                });
                setSignalRConnected(true);
              },
              onCompleted: (result: VideoGenerationResult) => {
                updateTask(taskId, {
                  status: 'completed',
                  progress: 100,
                  result,
                  updatedAt: new Date().toISOString(),
                });
                setIsGenerating(false);
              },
              onFailed: (error: string, isRetryable: boolean) => {
                updateTask(taskId, {
                  status: 'failed',
                  error,
                  message: isRetryable ? 'Failed (retryable)' : 'Failed',
                  updatedAt: new Date().toISOString(),
                });
                setError(error);
                setIsGenerating(false);
              },
            }
          );

          // Reset SignalR error count on success
          signalRErrorCount.current = 0;
          return;
        } catch (error) {
          console.warn('Failed to use progress tracking, falling back to polling:', error);
          signalRErrorCount.current++;
          setSignalRConnected(false);
          
          if (!fallbackToPolling) {
            throw error;
          }
          // Continue with fallback
        }
      }

      // Fallback to traditional polling approach
      const response = await fetch('/api/videos/generate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          prompt,
          model: settings.model,
          duration: settings.duration,
          size: settings.size,
          fps: settings.fps,
          style: settings.style,
          response_format: settings.responseFormat,
        }),
      });

      if (!response.ok) {
        let errorData: unknown;
        try {
          errorData = await response.json() as unknown;
        } catch {
          errorData = { error: response.statusText };
        }
        
        // Create a mock HTTP error that the SDK can handle properly
        const httpError = {
          response: {
            status: response.status,
            data: errorData,
            headers: Object.fromEntries(response.headers.entries())
          },
          message: response.statusText,
          request: { url: '/api/videos/generate', method: 'POST' }
        };
        
        // This will automatically throw the appropriate ConduitError subclass
        handleApiError(httpError, '/api/videos/generate', 'POST');
      }

      const data = await response.json() as AsyncVideoGenerationResponse;
      
      // Create task and start polling
      const newTask: VideoTask = {
        id: data.task_id,
        prompt,
        status: 'pending',
        progress: 0,
        message: data.message,
        estimatedTimeToCompletion: data.estimated_time_to_completion,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        settings,
        retryCount: 0,
        retryHistory: [],
      };
      
      addTask(newTask);

      // Start legacy polling
      pollingIntervalRef.current = setInterval(() => {
        void (async () => {
          try {
            const statusResponse = await fetch(`/api/videos/tasks/${data.task_id}`);
            if (!statusResponse.ok) {
              throw new Error('Failed to get task status');
            }
            
            const status = await statusResponse.json() as AsyncVideoGenerationResponse;
            
            updateTask(data.task_id, {
              status: status.status === 'Processing' ? 'running' : status.status.toLowerCase() as VideoTask['status'],
              progress: status.progress,
              message: status.message,
              updatedAt: status.updated_at,
            });

            if (['Completed', 'Failed', 'Cancelled', 'TimedOut'].includes(status.status)) {
              if (pollingIntervalRef.current) {
                clearInterval(pollingIntervalRef.current);
                pollingIntervalRef.current = null;
              }
              setIsGenerating(false);
              
              if (status.status === 'Completed' && status.result) {
                updateTask(data.task_id, {
                  status: 'completed',
                  result: status.result,
                });
              } else if (status.status === 'Failed') {
                setError(status.error ?? 'Video generation failed');
              }
            }
          } catch (error) {
            const errorMessage = handleError(error, 'check task status');
            if (pollingIntervalRef.current) {
              clearInterval(pollingIntervalRef.current);
              pollingIntervalRef.current = null;
            }
            setIsGenerating(false);
            setError(errorMessage);
          }
        })();
      }, 2000);

    } catch (error) {
      // Use enhanced error handler with toast notifications
      const errorMessage = handleError(error, 'generate video');
      setError(errorMessage);
      setIsGenerating(false);
      
      // Special handling for balance errors
      if (shouldShowBalanceWarning(error)) {
        setError('Please add credits to your account to generate videos.');
      }
    }
  }, [
    addTask,
    updateTask,
    setError,
    useProgressTracking,
    fallbackToPolling,
    handleError
  ]);

  const cancelGeneration = useCallback(async (taskId: string) => {
    try {
      const response = await fetch(`/api/videos/tasks/${taskId}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        throw new Error(`Failed to cancel task: ${response.statusText}`);
      }

      updateTask(taskId, { status: 'cancelled' });
      
      // Stop polling if active
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
        setIsGenerating(false);
      }
    } catch (error) {
      console.error('Error cancelling task:', error);
      setError(error instanceof Error ? error.message : 'Failed to cancel task');
    }
  }, [updateTask, setError]);

  return {
    generateVideo,
    cancelGeneration,
    isGenerating,
    isRetrying,
    signalRConnected,
    isProgressTrackingEnabled: useProgressTracking && signalRErrorCount.current < maxSignalRErrors,
  };
}
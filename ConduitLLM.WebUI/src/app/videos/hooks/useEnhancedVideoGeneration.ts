import { useState, useCallback, useRef, useEffect } from 'react';
import { useVideoStore } from './useVideoStore';
import { videoSignalRClient } from '@/lib/client/videoSignalRClient';
import type { 
  VideoSettings, 
  VideoTask, 
  AsyncVideoGenerationResponse,
  VideoGenerationResult
} from '../types';
import { 
  createToastErrorHandler, 
  shouldShowBalanceWarning,
  handleApiError
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
      void videoSignalRClient.disconnect();
    };
  }, []);

  const generateVideo = useCallback(async ({ prompt, settings, dynamicParameters }: GenerateVideoParams) => {
    setIsGenerating(true);
    setError(null);

    try {
      // Call the API to start video generation and get task ID + token
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
          // Include dynamic parameters from the UI
          ...dynamicParameters,
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
      
      // Create task in store
      const newTask: VideoTask = {
        id: data.task_id,
        prompt,
        status: 'pending',
        progress: 0,
        message: data.message ?? 'Initializing video generation...',
        estimatedTimeToCompletion: data.estimated_time_to_completion,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        settings,
        retryCount: 0,
        retryHistory: [],
      };
      
      addTask(newTask);

      // Try to use SignalR with ephemeral key
      // The client will get its own ephemeral key internally
      try {
        await videoSignalRClient.connect(
          data.task_id,
          undefined, // Let the client get its own ephemeral key
          {
            onProgress: (update) => {
              updateTask(data.task_id, {
                status: update.status === 'Processing' ? 'running' : update.status.toLowerCase() as VideoTask['status'],
                progress: update.progress ?? 0,
                message: update.message,
                updatedAt: new Date().toISOString(),
              });
            },
            onCompleted: (videoUrl) => {
              updateTask(data.task_id, {
                status: 'completed',
                progress: 100,
                result: {
                  created: Date.now(),
                  data: [{ url: videoUrl }]
                },
                updatedAt: new Date().toISOString(),
              });
              setIsGenerating(false);
            },
            onFailed: (error) => {
              updateTask(data.task_id, {
                status: 'failed',
                error,
                message: 'Video generation failed',
                updatedAt: new Date().toISOString(),
              });
              setError(error);
              setIsGenerating(false);
            },
          }
        );
        setSignalRConnected(true);
        signalRErrorCount.current = 0;
        return; // SignalR will handle updates
      } catch (signalRError) {
        console.warn('Failed to connect to SignalR, falling back to polling:', signalRError);
        signalRErrorCount.current++;
        setSignalRConnected(false);
        // Continue with polling fallback
      }

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

            // Check for both uppercase and lowercase status values
            const normalizedStatus = status.status.toLowerCase();
            if (['completed', 'failed', 'cancelled', 'timedout'].includes(normalizedStatus)) {
              if (pollingIntervalRef.current) {
                clearInterval(pollingIntervalRef.current);
                pollingIntervalRef.current = null;
              }
              setIsGenerating(false);
              
              if (normalizedStatus === 'completed' && status.result) {
                // Parse the result JSON string
                interface VideoResultData {
                  VideoUrl?: string;
                  videoUrl?: string;
                  Duration?: number;
                  Resolution?: string;
                  FileSize?: number;
                  Model?: string;
                }
                
                let parsedResult: VideoResultData;
                try {
                  parsedResult = typeof status.result === 'string' 
                    ? JSON.parse(status.result) as VideoResultData
                    : status.result as VideoResultData;
                } catch (e) {
                  console.error('Failed to parse video result:', e);
                  parsedResult = status.result as VideoResultData;
                }
                
                console.warn('Parsed video result:', parsedResult);
                
                // Transform to expected VideoGenerationResult format
                const videoResult: VideoGenerationResult = {
                  created: Date.now() / 1000,
                  data: [{
                    url: parsedResult.VideoUrl ?? parsedResult.videoUrl ?? '',
                    metadata: {
                      duration: parsedResult.Duration ?? 0,
                      resolution: parsedResult.Resolution ?? '',
                      file_size_bytes: parsedResult.FileSize ?? 0,
                    }
                  }],
                  model: parsedResult.Model ?? '',
                };
                
                console.warn('Transformed video result:', videoResult);
                
                updateTask(data.task_id, {
                  status: 'completed',
                  result: videoResult,
                });
              } else if (normalizedStatus === 'failed') {
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
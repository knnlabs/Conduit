import { useState, useCallback, useRef } from 'react';
import { useVideoStore } from './useVideoStore';
import { calculateRetryDelay, canRetry, type VideoSettings, type VideoTask, type VideoGenerationResult } from '../types';

interface GenerateVideoParams {
  prompt: string;
  settings: VideoSettings;
}

interface TaskStatusResponse {
  status: string;
  progress: number;
  message?: string;
  estimatedTimeToCompletion?: number;
  updatedAt?: string;
  result?: unknown;
  error?: string;
}

interface GenerateVideoResponse {
  taskId: string;
  message?: string;
  estimatedTimeToCompletion?: number;
  createdAt?: string;
  updatedAt?: string;
}

// Helper function to convert snake_case API response to camelCase
function mapTaskStatusResponse(apiResponse: Record<string, unknown>): TaskStatusResponse {
  return {
    status: apiResponse.status as string,
    progress: apiResponse.progress as number,
    message: apiResponse.message as string | undefined,
    estimatedTimeToCompletion: apiResponse.estimated_time_to_completion as number | undefined,
    updatedAt: apiResponse.updated_at as string | undefined,
    result: apiResponse.result,
    error: apiResponse.error as string | undefined,
  };
}

function mapGenerateVideoResponse(apiResponse: Record<string, unknown>): GenerateVideoResponse {
  return {
    taskId: apiResponse.task_id as string,
    message: apiResponse.message as string | undefined,
    estimatedTimeToCompletion: apiResponse.estimated_time_to_completion as number | undefined,
    createdAt: apiResponse.created_at as string | undefined,
    updatedAt: apiResponse.updated_at as string | undefined,
  };
}

interface ErrorResponse {
  error: string;
}

// Helper function to get user-friendly error messages
function getErrorMessage(status: string, error?: string): string {
  switch (status.toLowerCase()) {
    case 'timedout':
      return 'Video generation timed out. Large videos may take longer - please try again.';
    case 'cancelled':
      return 'Video generation was cancelled.';
    case 'failed':
      return error ?? 'Video generation failed. Please check your prompt and try again.';
    default:
      return error ?? 'An unexpected error occurred.';
  }
}

// Validate state transitions to prevent invalid state changes
function isValidStateTransition(currentStatus: VideoTask['status'], newStatus: VideoTask['status']): boolean {
  // Terminal states cannot transition to anything
  if (['completed', 'failed', 'cancelled', 'timedout'].includes(currentStatus)) {
    return false;
  }
  
  // Valid transitions
  const validTransitions: Record<VideoTask['status'], VideoTask['status'][]> = {
    'pending': ['running', 'cancelled', 'failed', 'timedout'],
    'running': ['completed', 'failed', 'cancelled', 'timedout'],
    'completed': [],
    'failed': [],
    'cancelled': [],
    'timedout': []
  };
  
  return validTransitions[currentStatus]?.includes(newStatus) ?? false;
}

export function useVideoGeneration() {
  const [isGenerating, setIsGenerating] = useState(false);
  const [isRetrying, setIsRetrying] = useState(false);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const { addTask, updateTask, setError, taskHistory, currentTask } = useVideoStore();

  const pollTaskStatus = useCallback(async (taskId: string) => {
    try {
      const response = await fetch(`/api/videos/tasks/${taskId}`);
      if (!response.ok) {
        throw new Error(`Failed to get task status: ${response.statusText}`);
      }
      
      const apiResponse = await response.json() as Record<string, unknown>;
      
      // Convert API response to camelCase
      const taskStatus = mapTaskStatusResponse(apiResponse);
      
      // Map API status to our internal status
      const mappedStatus = taskStatus.status === 'Processing' ? 'running' : 
                          taskStatus.status.toLowerCase() as VideoTask['status'];
      
      // Get current task to validate state transition
      const currentTask = taskHistory.find(t => t.id === taskId);
      if (currentTask && !isValidStateTransition(currentTask.status, mappedStatus)) {
        console.warn(`Invalid state transition attempted: ${currentTask.status} -> ${mappedStatus}`);
        return false; // Continue polling but skip invalid update
      }
      
      // Update task in store
      updateTask(taskId, {
        status: mappedStatus,
        progress: taskStatus.progress,
        message: taskStatus.message,
        estimatedTimeToCompletion: taskStatus.estimatedTimeToCompletion,
        updatedAt: taskStatus.updatedAt,
      });

      // Check if task is complete
      if (taskStatus.status === 'Completed') {
        if (taskStatus.result && (!currentTask || isValidStateTransition(currentTask.status, 'completed'))) {
          updateTask(taskId, {
            status: 'completed',
            result: taskStatus.result as VideoGenerationResult,
          });
        }
        return true; // Stop polling
      } else if (taskStatus.status === 'Failed') {
        if (!currentTask || isValidStateTransition(currentTask.status, 'failed')) {
          updateTask(taskId, {
            status: 'failed',
            error: getErrorMessage('failed', taskStatus.error),
          });
          setError(getErrorMessage('failed', taskStatus.error));
        }
        return true; // Stop polling
      } else if (taskStatus.status === 'Cancelled') {
        if (!currentTask || isValidStateTransition(currentTask.status, 'cancelled')) {
          updateTask(taskId, {
            status: 'cancelled',
            error: getErrorMessage('cancelled'),
          });
          setError(getErrorMessage('cancelled'));
        }
        return true; // Stop polling
      } else if (taskStatus.status === 'TimedOut') {
        if (!currentTask || isValidStateTransition(currentTask.status, 'timedout')) {
          updateTask(taskId, {
            status: 'timedout',
            error: getErrorMessage('timedout'),
          });
          setError(getErrorMessage('timedout'));
        }
        return true; // Stop polling
      }
      
      return false; // Continue polling
    } catch (error) {
      console.error('Error polling task status:', error);
      updateTask(taskId, {
        status: 'failed',
        error: error instanceof Error ? error.message : 'Failed to get task status',
      });
      setError(error instanceof Error ? error.message : 'Failed to get task status');
      return true; // Stop polling on error
    }
  }, [updateTask, setError, taskHistory]);

  const generateVideo = useCallback(async ({ prompt, settings }: GenerateVideoParams) => {
    setIsGenerating(true);
    setError(null);

    try {
      // Start video generation
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
        const errorData = await response.json().catch(() => null) as ErrorResponse | null;
        throw new Error(errorData?.error ?? `Failed to generate video: ${response.statusText}`);
      }

      const apiData = await response.json() as Record<string, unknown>;
      
      // Convert API response to camelCase
      const data = mapGenerateVideoResponse(apiData);
      
      // Create new task
      const newTask: VideoTask = {
        id: data.taskId,
        prompt,
        status: 'pending',
        progress: 0,
        message: data.message,
        estimatedTimeToCompletion: data.estimatedTimeToCompletion,
        createdAt: data.createdAt ?? new Date().toISOString(),
        updatedAt: data.updatedAt ?? new Date().toISOString(),
        settings,
        retryCount: 0,
        retryHistory: [],
      };
      
      addTask(newTask);

      // Start polling for status
      pollingIntervalRef.current = setInterval(() => {
        void (async () => {
          const shouldStop = await pollTaskStatus(data.taskId);
          if (shouldStop && pollingIntervalRef.current) {
            clearInterval(pollingIntervalRef.current);
            pollingIntervalRef.current = null;
            setIsGenerating(false);
          }
        })();
      }, 2000); // Poll every 2 seconds

    } catch (error) {
      console.error('Error generating video:', error);
      setError(error instanceof Error ? error.message : 'Failed to generate video');
      setIsGenerating(false);
    }
  }, [addTask, setError, pollTaskStatus]);

  const retryGeneration = useCallback(async (task: VideoTask) => {
    // Prevent concurrent retries
    if (isRetrying || isGenerating) {
      setError('Another operation is already in progress');
      return;
    }

    if (!canRetry(task)) {
      setError('This task cannot be retried');
      return;
    }

    setIsRetrying(true);
    try {
      // Calculate retry delay
      const delay = calculateRetryDelay(task.retryCount);
      
      // Update task to show retry is pending
      const retryHistory = [...task.retryHistory];
      if (task.error) {
        retryHistory.push({
          attemptNumber: task.retryCount + 1,
          timestamp: new Date().toISOString(),
          error: task.error,
        });
      }

      updateTask(task.id, {
        status: 'pending',
        error: undefined,
        message: `Retrying in ${delay / 1000}s...`,
        retryCount: task.retryCount + 1,
        lastRetryAt: new Date().toISOString(),
        retryHistory,
      });

      // Wait for the retry delay
      await new Promise(resolve => setTimeout(resolve, delay));

      // Update the task ID for the retry attempt
      const retryTask = currentTask?.id === task.id ? currentTask : task;
      
      // Update message to indicate retry is starting
      updateTask(retryTask.id, {
        message: 'Starting retry...',
      });

      // Start polling for the same task ID (backend handles retry)
      setIsGenerating(true);
      pollingIntervalRef.current = setInterval(() => {
        void (async () => {
          const shouldStop = await pollTaskStatus(retryTask.id);
          if (shouldStop && pollingIntervalRef.current) {
            clearInterval(pollingIntervalRef.current);
            pollingIntervalRef.current = null;
            setIsGenerating(false);
          }
        })();
      }, 2000);
    } finally {
      setIsRetrying(false);
    }
  }, [isRetrying, isGenerating, updateTask, setError, pollTaskStatus, currentTask]);

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

  // Cleanup on unmount
  const cleanup = useCallback(() => {
    if (pollingIntervalRef.current) {
      clearInterval(pollingIntervalRef.current);
      pollingIntervalRef.current = null;
    }
  }, []);

  return {
    generateVideo,
    retryGeneration,
    cancelGeneration,
    isGenerating,
    isRetrying,
    cleanup,
  };
}
import { useState, useCallback, useRef } from 'react';
import { useVideoStore } from './useVideoStore';
import type { VideoSettings, VideoTask, VideoGenerationResult } from '../types';

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

export function useVideoGeneration() {
  const [isGenerating, setIsGenerating] = useState(false);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const { addTask, updateTask, setError } = useVideoStore();

  const pollTaskStatus = useCallback(async (taskId: string) => {
    try {
      const response = await fetch(`/api/videos/tasks/${taskId}`);
      if (!response.ok) {
        throw new Error(`Failed to get task status: ${response.statusText}`);
      }
      
      const apiResponse = await response.json() as Record<string, unknown>;
      
      // Convert API response to camelCase
      const taskStatus = mapTaskStatusResponse(apiResponse);
      
      // Update task in store
      updateTask(taskId, {
        status: taskStatus.status.toLowerCase() as VideoTask['status'],
        progress: taskStatus.progress,
        message: taskStatus.message,
        estimatedTimeToCompletion: taskStatus.estimatedTimeToCompletion,
        updatedAt: taskStatus.updatedAt,
      });

      // Check if task is complete
      if (taskStatus.status === 'Completed') {
        if (taskStatus.result) {
          updateTask(taskId, {
            status: 'completed',
            result: taskStatus.result as VideoGenerationResult,
          });
        }
        return true; // Stop polling
      } else if (taskStatus.status === 'Failed' || taskStatus.status === 'Cancelled' || taskStatus.status === 'TimedOut') {
        updateTask(taskId, {
          status: 'failed',
          error: taskStatus.error ?? `Task ${taskStatus.status.toLowerCase()}`,
        });
        setError(taskStatus.error ?? `Video generation ${taskStatus.status.toLowerCase()}`);
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
  }, [updateTask, setError]);

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
    cancelGeneration,
    isGenerating,
    cleanup,
  };
}
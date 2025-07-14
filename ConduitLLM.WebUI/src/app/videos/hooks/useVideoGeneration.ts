import { useState, useCallback, useRef } from 'react';
import { useVideoStore } from './useVideoStore';
import type { VideoSettings, VideoTask } from '../types';

interface GenerateVideoParams {
  prompt: string;
  settings: VideoSettings;
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
      
      const taskStatus = await response.json();
      
      // Update task in store
      updateTask(taskId, {
        status: taskStatus.status.toLowerCase(),
        progress: taskStatus.progress,
        message: taskStatus.message,
        estimatedTimeToCompletion: taskStatus.estimated_time_to_completion,
        updatedAt: taskStatus.updated_at,
      });

      // Check if task is complete
      if (taskStatus.status === 'Completed') {
        if (taskStatus.result) {
          updateTask(taskId, {
            status: 'completed',
            result: taskStatus.result,
          });
        }
        return true; // Stop polling
      } else if (taskStatus.status === 'Failed' || taskStatus.status === 'Cancelled' || taskStatus.status === 'TimedOut') {
        updateTask(taskId, {
          status: 'failed',
          error: taskStatus.error || `Task ${taskStatus.status.toLowerCase()}`,
        });
        setError(taskStatus.error || `Video generation ${taskStatus.status.toLowerCase()}`);
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
          response_format: settings.response_format,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        throw new Error(errorData?.error || `Failed to generate video: ${response.statusText}`);
      }

      const data = await response.json();
      
      // Create new task
      const newTask: VideoTask = {
        id: data.task_id,
        prompt,
        status: 'pending',
        progress: 0,
        message: data.message,
        estimatedTimeToCompletion: data.estimated_time_to_completion,
        createdAt: data.created_at || new Date().toISOString(),
        updatedAt: data.updated_at || new Date().toISOString(),
        settings,
      };
      
      addTask(newTask);

      // Start polling for status
      pollingIntervalRef.current = setInterval(async () => {
        const shouldStop = await pollTaskStatus(data.task_id);
        if (shouldStop && pollingIntervalRef.current) {
          clearInterval(pollingIntervalRef.current);
          pollingIntervalRef.current = null;
          setIsGenerating(false);
        }
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
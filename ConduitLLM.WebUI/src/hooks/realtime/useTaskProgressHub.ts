import { useEffect, useCallback, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { 
  getSDKSignalRManager, 
  VideoGenerationProgress, 
  ImageGenerationProgress 
} from '@/lib/signalr/SDKSignalRManager';
import { logger } from '@/lib/utils/logging';

export interface TaskProgress {
  taskId: string;
  type: 'video' | 'image';
  status: 'queued' | 'processing' | 'completed' | 'failed';
  progress: number;
  estimatedTimeRemaining?: number;
  resultUrl?: string;
  error?: string;
  updatedAt: Date;
}

export interface UseTaskProgressHubOptions {
  enabled?: boolean;
  taskIds?: string[];
  onVideoProgress?: (progress: VideoGenerationProgress) => void;
  onImageProgress?: (progress: ImageGenerationProgress) => void;
  onTaskComplete?: (task: TaskProgress) => void;
  onTaskFailed?: (task: TaskProgress) => void;
}

export function useTaskProgressHub(options: UseTaskProgressHubOptions = {}) {
  const { 
    enabled = true, 
    taskIds = [],
    onVideoProgress,
    onImageProgress,
    onTaskComplete,
    onTaskFailed,
  } = options;
  
  const queryClient = useQueryClient();
  const [activeTasks, setActiveTasks] = useState<Map<string, TaskProgress>>(new Map());

  // Handle video generation progress
  const handleVideoProgress = useCallback((progress: VideoGenerationProgress) => {
    logger.debug('Video generation progress:', progress);

    // Filter by taskIds if specified
    if (taskIds.length > 0 && !taskIds.includes(progress.taskId)) {
      return;
    }

    // Call custom handler
    onVideoProgress?.(progress);

    // Update active tasks
    setActiveTasks(prev => {
      const updated = new Map(prev);
      const task: TaskProgress = {
        taskId: progress.taskId,
        type: 'video',
        status: progress.status,
        progress: progress.progress,
        estimatedTimeRemaining: progress.estimatedTimeRemaining,
        resultUrl: progress.resultUrl,
        error: progress.error,
        updatedAt: new Date(),
      };
      updated.set(progress.taskId, task);

      // Handle completion/failure
      if (progress.status === 'completed') {
        onTaskComplete?.(task);
        // Invalidate video generation queries
        queryClient.invalidateQueries({ queryKey: ['videos', progress.taskId] });
      } else if (progress.status === 'failed') {
        onTaskFailed?.(task);
      }

      return updated;
    });
  }, [taskIds, onVideoProgress, onTaskComplete, onTaskFailed, queryClient]);

  // Handle image generation progress
  const handleImageProgress = useCallback((progress: ImageGenerationProgress) => {
    logger.debug('Image generation progress:', progress);

    // Filter by taskIds if specified
    if (taskIds.length > 0 && !taskIds.includes(progress.taskId)) {
      return;
    }

    // Call custom handler
    onImageProgress?.(progress);

    // Update active tasks
    setActiveTasks(prev => {
      const updated = new Map(prev);
      const task: TaskProgress = {
        taskId: progress.taskId,
        type: 'image',
        status: progress.status,
        progress: progress.progress,
        resultUrl: progress.resultUrl,
        error: progress.error,
        updatedAt: new Date(),
      };
      updated.set(progress.taskId, task);

      // Handle completion/failure
      if (progress.status === 'completed') {
        onTaskComplete?.(task);
        // Invalidate image generation queries
        queryClient.invalidateQueries({ queryKey: ['images', progress.taskId] });
      } else if (progress.status === 'failed') {
        onTaskFailed?.(task);
      }

      return updated;
    });
  }, [taskIds, onImageProgress, onTaskComplete, onTaskFailed, queryClient]);

  useEffect(() => {
    if (!enabled) return;

    try {
      // Get SignalR manager
      const signalRManager = getSDKSignalRManager();
      
      // Register event handlers
      signalRManager.on('onVideoGenerationProgress', handleVideoProgress);
      signalRManager.on('onImageGenerationProgress', handleImageProgress);

      logger.info('Task progress hub listeners registered');

      // Cleanup
      return () => {
        signalRManager.off('onVideoGenerationProgress');
        signalRManager.off('onImageGenerationProgress');
        logger.info('Task progress hub listeners unregistered');
      };
    } catch (error) {
      logger.error('Failed to setup task progress hub:', error);
    }
  }, [enabled, handleVideoProgress, handleImageProgress]);

  // Get specific task progress
  const getTaskProgress = useCallback((taskId: string): TaskProgress | undefined => {
    return activeTasks.get(taskId);
  }, [activeTasks]);

  // Get all active tasks
  const getAllTasks = useCallback((): TaskProgress[] => {
    return Array.from(activeTasks.values());
  }, [activeTasks]);

  // Get tasks by type
  const getTasksByType = useCallback((type: 'video' | 'image'): TaskProgress[] => {
    return Array.from(activeTasks.values()).filter(task => task.type === type);
  }, [activeTasks]);

  // Clear completed tasks
  const clearCompletedTasks = useCallback(() => {
    setActiveTasks(prev => {
      const updated = new Map(prev);
      for (const [taskId, task] of updated) {
        if (task.status === 'completed' || task.status === 'failed') {
          updated.delete(taskId);
        }
      }
      return updated;
    });
  }, []);

  // Clear specific task
  const clearTask = useCallback((taskId: string) => {
    setActiveTasks(prev => {
      const updated = new Map(prev);
      updated.delete(taskId);
      return updated;
    });
  }, []);

  return {
    activeTasks: getAllTasks(),
    videoTasks: getTasksByType('video'),
    imageTasks: getTasksByType('image'),
    getTaskProgress,
    clearCompletedTasks,
    clearTask,
    isConnected: enabled,
  };
}

// Hook for monitoring a specific task
export function useTaskProgress(taskId: string | null, type: 'video' | 'image') {
  const [task, setTask] = useState<TaskProgress | null>(null);
  const [isComplete, setIsComplete] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleProgress = useCallback((progress: VideoGenerationProgress | ImageGenerationProgress) => {
    if (progress.taskId !== taskId) return;

    const taskProgress: TaskProgress = {
      taskId: progress.taskId,
      type,
      status: progress.status,
      progress: progress.progress,
      estimatedTimeRemaining: (progress as VideoGenerationProgress).estimatedTimeRemaining,
      resultUrl: progress.resultUrl,
      error: progress.error,
      updatedAt: new Date(),
    };

    setTask(taskProgress);
    
    if (progress.status === 'completed') {
      setIsComplete(true);
    } else if (progress.status === 'failed') {
      setError(progress.error || 'Task failed');
    }
  }, [taskId, type]);

  const { activeTasks } = useTaskProgressHub({
    enabled: !!taskId,
    taskIds: taskId ? [taskId] : [],
    onVideoProgress: type === 'video' ? handleProgress : undefined,
    onImageProgress: type === 'image' ? handleProgress : undefined,
  });

  // Initialize task from active tasks if available
  useEffect(() => {
    if (taskId && !task) {
      const activeTask = activeTasks.find(t => t.taskId === taskId);
      if (activeTask) {
        setTask(activeTask);
        setIsComplete(activeTask.status === 'completed');
        setError(activeTask.error || null);
      }
    }
  }, [taskId, task, activeTasks]);

  return {
    task,
    isComplete,
    error,
    progress: task?.progress || 0,
    status: task?.status || 'queued',
    resultUrl: task?.resultUrl,
  };
}
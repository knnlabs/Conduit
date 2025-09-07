import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { type VideoStoreState, type VideoTask, type VideoSettings } from '../types';

const LOCAL_STORAGE_KEY = 'conduit-video-generation';

export const useVideoStore = create<VideoStoreState>()(
  persist(
    (set) => ({
      // UI State
      error: null,

      // Settings
      settings: {
        model: '',
      },

      // Tasks
      currentTask: null,
      taskHistory: [],

      // Actions
      updateSettings: (updates: Partial<VideoSettings>) =>
        set((state) => ({
          settings: { ...state.settings, ...updates },
        })),

      setError: (error: string | null) => set({ error }),

      addTask: (task: VideoTask) =>
        set((state) => {
          // Check if task already exists in history
          const existingTaskIndex = state.taskHistory.findIndex(t => t.id === task.id);
          let newHistory;
          
          if (existingTaskIndex !== -1) {
            // Update existing task instead of adding duplicate
            newHistory = [...state.taskHistory];
            newHistory[existingTaskIndex] = task;
          } else {
            // Add new task to the beginning
            newHistory = [task, ...state.taskHistory].slice(0, 20); // Keep last 20 tasks
          }
          
          return {
            currentTask: task,
            taskHistory: newHistory,
          };
        }),

      updateTask: (taskId: string, updates: Partial<VideoTask>) =>
        set((state) => {
          // Check if task exists in history
          const taskExists = state.taskHistory.some(task => task.id === taskId);
          
          let updatedHistory;
          if (taskExists) {
            // Update existing task
            updatedHistory = state.taskHistory.map((task) =>
              task.id === taskId ? { ...task, ...updates, updatedAt: new Date().toISOString() } : task
            );
          } else {
            // Add as new task if it doesn't exist (shouldn't happen normally)
            const newTask = {
              id: taskId,
              prompt: '',
              status: 'pending',
              progress: 0,
              createdAt: new Date().toISOString(),
              updatedAt: new Date().toISOString(),
              settings: state.settings,
              retryCount: 0,
              retryHistory: [],
              ...updates,
            } as VideoTask;
            updatedHistory = [newTask, ...state.taskHistory].slice(0, 20);
          }
          
          let updatedCurrent = state.currentTask?.id === taskId
            ? { ...state.currentTask, ...updates, updatedAt: new Date().toISOString() }
            : state.currentTask;
          
          // Clear currentTask if it's completed or failed
          if (updatedCurrent && (updatedCurrent.status === 'completed' || updatedCurrent.status === 'failed' || updatedCurrent.status === 'cancelled')) {
            updatedCurrent = null;
          }

          return {
            currentTask: updatedCurrent,
            taskHistory: updatedHistory,
          };
        }),

      removeTask: (taskId: string) =>
        set((state) => ({
          currentTask: state.currentTask?.id === taskId ? null : state.currentTask,
          taskHistory: state.taskHistory.filter((task) => task.id !== taskId),
        })),

      clearHistory: () => set({ taskHistory: [], currentTask: null }),
    }),
    {
      name: LOCAL_STORAGE_KEY,
      partialize: (state) => ({
        settings: state.settings,
        taskHistory: state.taskHistory.filter(
          (task) => task.status === 'completed' || task.status === 'failed'
        ),
      }),
    }
  )
);
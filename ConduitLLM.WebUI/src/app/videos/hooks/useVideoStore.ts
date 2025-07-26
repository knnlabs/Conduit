import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { VideoDefaults, type VideoStoreState, type VideoTask, type VideoSettings } from '../types';

const LOCAL_STORAGE_KEY = 'conduit-video-generation';

export const useVideoStore = create<VideoStoreState>()(
  persist(
    (set) => ({
      // UI State
      settingsVisible: false,
      error: null,

      // Settings
      settings: {
        model: '',
        duration: VideoDefaults.DURATION,
        size: VideoDefaults.RESOLUTION,
        fps: VideoDefaults.FPS,
        style: undefined,
        responseFormat: VideoDefaults.RESPONSE_FORMAT,
      },

      // Tasks
      currentTask: null,
      taskHistory: [],

      // Actions
      toggleSettings: () => set((state) => ({ settingsVisible: !state.settingsVisible })),

      updateSettings: (updates: Partial<VideoSettings>) =>
        set((state) => ({
          settings: { ...state.settings, ...updates },
        })),

      setError: (error: string | null) => set({ error }),

      addTask: (task: VideoTask) =>
        set((state) => ({
          currentTask: task,
          taskHistory: [task, ...state.taskHistory].slice(0, 20), // Keep last 20 tasks
        })),

      updateTask: (taskId: string, updates: Partial<VideoTask>) =>
        set((state) => {
          const updatedHistory = state.taskHistory.map((task) =>
            task.id === taskId ? { ...task, ...updates, updatedAt: new Date().toISOString() } : task
          );
          
          const updatedCurrent = state.currentTask?.id === taskId
            ? { ...state.currentTask, ...updates, updatedAt: new Date().toISOString() }
            : state.currentTask;

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
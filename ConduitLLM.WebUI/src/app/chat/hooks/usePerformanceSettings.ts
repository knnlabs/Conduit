import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface PerformanceSettings {
  // Display settings
  showTokensPerSecond: boolean;
  showLatency: boolean;
  showTokenCount: boolean;
  
  // Performance tracking settings
  trackPerformanceMetrics: boolean;
  useServerMetrics: boolean; // Prefer server metrics over client calculations
  
  // Actions
  updateSettings: (settings: Partial<PerformanceSettings>) => void;
  resetToDefaults: () => void;
}

const DEFAULT_SETTINGS: Omit<PerformanceSettings, 'updateSettings' | 'resetToDefaults'> = {
  showTokensPerSecond: true,
  showLatency: true,
  showTokenCount: true,
  trackPerformanceMetrics: true,
  useServerMetrics: true,
};

export const usePerformanceSettings = create<PerformanceSettings>()(
  persist(
    (set) => ({
      ...DEFAULT_SETTINGS,
      
      updateSettings: (settings) =>
        set((state) => ({
          ...state,
          ...settings,
        })),
        
      resetToDefaults: () =>
        set(() => ({
          ...DEFAULT_SETTINGS,
        })),
    }),
    {
      name: 'chat-performance-settings',
    }
  )
);
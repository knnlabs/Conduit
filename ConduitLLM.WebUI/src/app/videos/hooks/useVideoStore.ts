import { create } from 'zustand';
import { createMediaStore, type MediaStore } from '@/app/hooks/createMediaStore';
import type { VideoTask, VideoSettings } from '../types';

const LOCAL_STORAGE_KEY = 'conduit-video-generation';

// Create the base store configuration
const videoStoreConfig = createMediaStore<VideoTask, VideoSettings>({
  name: LOCAL_STORAGE_KEY,
  initialSettings: {
    model: '',
  },
  maxHistorySize: 20,
  persistHistory: true,
  partializeState: (state) => ({
    settings: state.settings,
    taskHistory: state.taskHistory.filter(
      (task) => task.status === 'completed' || task.status === 'failed'
    ),
  }),
});

// Extend the base store type with any video-specific functionality if needed
export type VideoStoreState = MediaStore<VideoTask, VideoSettings>;

// Create the actual store
export const useVideoStore = create<VideoStoreState>()(videoStoreConfig);
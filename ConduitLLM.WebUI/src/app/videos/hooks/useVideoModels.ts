import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';

async function fetchVideoModels(): Promise<VideoModel[]> {
  const response = await fetch('/api/discovery/models?capability=video_generation');
  if (!response.ok) {
    throw new Error(`Failed to fetch video models: ${response.statusText}`);
  }
  
  const data = await response.json();
  
  // Filter and transform models that support video generation
  return data.data
    .filter((model: any) => model.capabilities?.video_generation === true)
    .map((model: any) => ({
      id: model.id,
      provider: model.provider,
      display_name: model.display_name || model.id,
      capabilities: {
        video_generation: model.capabilities.video_generation,
        max_duration: model.capabilities.max_video_duration_seconds,
        supported_resolutions: model.capabilities.supported_video_resolutions,
        supported_fps: model.capabilities.supported_fps,
        supports_custom_styles: model.capabilities.supports_custom_styles,
        supports_seed: model.capabilities.supports_seed,
        max_videos: model.capabilities.max_videos || 1,
      },
    }));
}

export function useVideoModels() {
  return useQuery<VideoModel[], Error>({
    queryKey: ['video-models'],
    queryFn: fetchVideoModels,
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 10 * 60 * 1000, // 10 minutes (was cacheTime in v4)
    retry: 3,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}
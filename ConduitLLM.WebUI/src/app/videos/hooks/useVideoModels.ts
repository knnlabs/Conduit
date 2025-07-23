import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';

interface ApiModelResponse {
  data: ApiModel[];
}

interface ApiModel {
  id: string;
  provider: string;
  display_name?: string;
  capabilities?: {
    video_generation?: {
      supported: boolean;
      max_duration_seconds?: number;
      supported_resolutions?: string[];
      supported_fps?: number[];
      supports_custom_styles?: boolean;
    };
  };
}

async function fetchVideoModels(): Promise<VideoModel[]> {
  const response = await fetch('/api/discovery/models?capability=video_generation');
  if (!response.ok) {
    throw new Error(`Failed to fetch video models: ${response.statusText}`);
  }
  
  const data = await response.json() as ApiModelResponse;
  
  // Filter and transform models that support video generation
  return data.data
    .filter((model: ApiModel) => model.capabilities?.video_generation?.supported === true)
    .map((model: ApiModel) => {
      const videoCapability = model.capabilities?.video_generation;
      return {
        id: model.id,
        provider: model.provider,
        displayName: model.display_name ?? model.id,
        capabilities: {
          videoGeneration: true,
          maxDuration: videoCapability?.max_duration_seconds,
          supportedResolutions: videoCapability?.supported_resolutions,
          supportedFps: videoCapability?.supported_fps,
          supportsCustomStyles: videoCapability?.supports_custom_styles,
          supportsSeed: false, // Not provided by API yet
          maxVideos: 1, // Default value
        },
      };
    });
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
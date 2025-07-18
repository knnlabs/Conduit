import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';

interface ApiModelResponse {
  data: ApiModel[];
}

interface ApiModel {
  id: string;
  provider: string;
  displayName?: string;
  capabilities?: {
    videoGeneration?: boolean;
    maxVideoDurationSeconds?: number;
    supportedVideoResolutions?: string[];
    supportedFps?: number[];
    supportsCustomStyles?: boolean;
    supportsSeed?: boolean;
    maxVideos?: number;
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
    .filter((model: ApiModel) => model.capabilities?.videoGeneration === true)
    .map((model: ApiModel) => {
      return {
        id: model.id,
        provider: model.provider,
        displayName: model.displayName ?? model.id,
        capabilities: {
          videoGeneration: model.capabilities?.videoGeneration ?? false,
          maxDuration: model.capabilities?.maxVideoDurationSeconds,
          supportedResolutions: model.capabilities?.supportedVideoResolutions,
          supportedFps: model.capabilities?.supportedFps,
          supportsCustomStyles: model.capabilities?.supportsCustomStyles,
          supportsSeed: model.capabilities?.supportsSeed,
          maxVideos: model.capabilities?.maxVideos ?? 1,
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
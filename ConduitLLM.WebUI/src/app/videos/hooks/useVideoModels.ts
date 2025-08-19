import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';
import { withAdminClient } from '@/lib/client/adminClient';

async function fetchVideoModels(): Promise<VideoModel[]> {
  // Fetch models from the Models endpoint
  const models = await withAdminClient(client => 
    client.models.list()
  );
  
  // Filter to only include video generation models (modelType === 3)
  const videoModels = models.filter((model) => 
    model.modelType === 3 && model.isActive !== false
  );
  
  return videoModels.map((model) => {
    // Check if capabilities support video generation
    const supportsVideo = model.capabilities?.supportsVideoGeneration === true;
    
    return {
      id: model.id?.toString() ?? 'unknown',
      provider: 'Video Provider', // Author info is not directly on Model, would need to fetch from series
      displayName: model.name ?? 'Unnamed Video Model',
      capabilities: {
        videoGeneration: supportsVideo,
        // Default values since models don't store detailed video capabilities yet
        maxDuration: 10, // seconds
        supportedResolutions: ['1280x720', '720x480'],
        supportedFps: [24, 30],
        supportsCustomStyles: true,
        supportsSeed: true,
        maxVideos: 1,
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
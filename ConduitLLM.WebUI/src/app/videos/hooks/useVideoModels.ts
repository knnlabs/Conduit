import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';
import { withAdminClient } from '@/lib/client/adminClient';

async function fetchVideoModels(): Promise<VideoModel[]> {
  // Fetch model mappings, models, and providers in parallel
  const [mappings, models, providersResponse] = await Promise.all([
    withAdminClient(client => client.modelMappings.list()),
    withAdminClient(client => client.models.list()),
    withAdminClient(client => client.providers.list(1, 100)) // Get up to 100 providers
  ]);
  
  // Create lookup maps for efficient access
  const modelsMap = new Map(models.map(m => [m.id, m]));
  const providersMap = new Map(providersResponse.items.map(p => [p.id, p]));
  
  // Filter mappings to only include enabled video generation models
  const videoMappings = mappings.filter(mapping => {
    // Must be enabled
    if (!mapping.isEnabled) return false;
    
    // Get the associated model
    const model = modelsMap.get(mapping.modelId);
    if (!model) return false;
    
    // Must be a video model (modelType === 3)
    if (model.modelType !== 3) return false;
    
    // Must support video generation capability
    if (!model.capabilities?.supportsVideoGeneration) return false;
    
    // Must be active
    if (model.isActive === false) return false;
    
    return true;
  });
  
  // Map to the expected format
  const videoModels: VideoModel[] = videoMappings.map(mapping => {
    const provider = providersMap.get(mapping.providerId);
    
    return {
      id: mapping.modelAlias, // Use the alias as the ID for API calls
      provider: provider?.providerName ?? 'Unknown Provider',
      displayName: mapping.modelAlias, // Use alias as display name (not optional in our case)
      capabilities: {
        videoGeneration: true, // Already filtered for this capability
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
  
  // Sort by provider name and then by display name, removing duplicates
  const uniqueModels = Array.from(
    new Map(videoModels.map(m => [m.id, m])).values()
  );
  
  return uniqueModels.sort((a, b) => {
    if (a.provider !== b.provider) {
      return a.provider.localeCompare(b.provider);
    }
    // Handle optional displayName
    const aDisplay = a.displayName ?? a.id;
    const bDisplay = b.displayName ?? b.id;
    return aDisplay.localeCompare(bDisplay);
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
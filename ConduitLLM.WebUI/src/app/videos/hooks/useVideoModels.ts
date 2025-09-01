import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';
import { withAdminClient } from '@/lib/client/adminClient';

async function fetchVideoModels(): Promise<VideoModel[]> {
  // Fetch model mappings and providers
  const [mappings, providersResponse] = await Promise.all([
    withAdminClient(client => client.modelMappings.list()),
    withAdminClient(client => client.providers.list(1, 100)) // Get up to 100 providers
  ]);
  
  // Create lookup map for providers
  const providersMap = new Map(providersResponse.items.map(p => [p.id, p]));
  
  // For now, return all enabled mappings as video models
  // We can't filter by capability without the Model data
  // TODO: Backend should provide enriched mappings with model capabilities
  const videoMappings = mappings.filter(mapping => {
    // Must be enabled
    return mapping.isEnabled;
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
      parameters: undefined, // TODO: Need backend to provide model parameters
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
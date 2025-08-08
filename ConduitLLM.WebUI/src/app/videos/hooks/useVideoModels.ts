import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';
import { ProviderType, type ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';

// Helper function to convert ProviderType enum to string
function getProviderName(providerType: ProviderType): string {
  const providerNames: Record<ProviderType, string> = {
    [ProviderType.OpenAI]: 'OpenAI',
    [ProviderType.Groq]: 'Groq',
    [ProviderType.Replicate]: 'Replicate',
    [ProviderType.Fireworks]: 'Fireworks',
    [ProviderType.OpenAICompatible]: 'OpenAI Compatible',
    [ProviderType.MiniMax]: 'MiniMax',
    [ProviderType.Ultravox]: 'Ultravox',
    [ProviderType.ElevenLabs]: 'ElevenLabs',
    [ProviderType.Cerebras]: 'Cerebras',
    [ProviderType.SambaNova]: 'SambaNova',
    [ProviderType.DeepInfra]: 'DeepInfra',
  };
  return providerNames[providerType] || `Provider ${providerType}`;
}

async function fetchVideoModels(): Promise<VideoModel[]> {
  const result = await withAdminClient(client => 
    client.modelMappings.list()
  );
  
  const mappings = result;
  
  // Filter to only include video generation capable models that are enabled
  const videoModels = mappings.filter((mapping: ModelProviderMappingDto) => 
    mapping.supportsVideoGeneration === true && mapping.isEnabled !== false
  );
  
  return videoModels.map((mapping: ModelProviderMappingDto) => {
    const providerDisplayName = mapping.provider?.displayName ?? 
      (mapping.provider?.providerType !== undefined ? getProviderName(mapping.provider.providerType) : 'Unknown');
    return {
      id: mapping.modelId,
      provider: providerDisplayName,
      displayName: `${mapping.modelId} (${providerDisplayName})`,
      capabilities: {
        videoGeneration: true,
        // Default values since model mappings don't store detailed video capabilities yet
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
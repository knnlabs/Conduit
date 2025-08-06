import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';
import { ProviderType } from '@knn_labs/conduit-admin-client';

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
  };
  return providerNames[providerType] || `Provider ${providerType}`;
}

interface ModelMapping {
  modelId: string;
  providerId: string;
  providerType?: ProviderType;
  provider?: {
    id: number;
    providerType: ProviderType;
    displayName: string;
    isEnabled: boolean;
  };
  maxContextLength?: number;
  supportsVideoGeneration?: boolean;
  isEnabled?: boolean;
}

async function fetchVideoModels(): Promise<VideoModel[]> {
  const response = await fetch('/api/model-mappings');
  if (!response.ok) {
    throw new Error(`Failed to fetch model mappings: ${response.statusText}`);
  }
  
  const mappings = await response.json() as ModelMapping[];
  
  // Filter to only include video generation capable models that are enabled
  const videoModels = mappings.filter((mapping: ModelMapping) => 
    mapping.supportsVideoGeneration === true && mapping.isEnabled !== false
  );
  
  return videoModels.map((mapping: ModelMapping) => {
    const providerDisplayName = mapping.provider?.displayName ?? 
      (mapping.providerType !== undefined ? getProviderName(mapping.providerType) : 'Unknown');
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
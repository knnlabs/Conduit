import { useQuery } from '@tanstack/react-query';
import type { VideoModel } from '../types';
import { ProviderType } from '@knn_labs/conduit-admin-client';

// Helper function to convert ProviderType enum to string
function getProviderName(providerType: ProviderType): string {
  const providerNames: Record<ProviderType, string> = {
    [ProviderType.OpenAI]: 'OpenAI',
    [ProviderType.Anthropic]: 'Anthropic',
    [ProviderType.AzureOpenAI]: 'Azure OpenAI',
    [ProviderType.Gemini]: 'Gemini',
    [ProviderType.VertexAI]: 'Vertex AI',
    [ProviderType.Cohere]: 'Cohere',
    [ProviderType.Mistral]: 'Mistral',
    [ProviderType.Groq]: 'Groq',
    [ProviderType.Ollama]: 'Ollama',
    [ProviderType.Replicate]: 'Replicate',
    [ProviderType.Fireworks]: 'Fireworks',
    [ProviderType.Bedrock]: 'Bedrock',
    [ProviderType.HuggingFace]: 'HuggingFace',
    [ProviderType.SageMaker]: 'SageMaker',
    [ProviderType.OpenRouter]: 'OpenRouter',
    [ProviderType.OpenAICompatible]: 'OpenAI Compatible',
    [ProviderType.MiniMax]: 'MiniMax',
    [ProviderType.Ultravox]: 'Ultravox',
    [ProviderType.ElevenLabs]: 'ElevenLabs',
    [ProviderType.GoogleCloud]: 'Google Cloud',
    [ProviderType.Cerebras]: 'Cerebras',
    [ProviderType.Unknown]: 'Unknown',
  };
  return providerNames[providerType] || `Provider ${providerType}`;
}

interface ModelMapping {
  modelId: string;
  providerId: string;
  providerType?: ProviderType;
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
    const providerName = mapping.providerType !== undefined ? getProviderName(mapping.providerType) : 'Unknown';
    return {
      id: mapping.modelId,
      provider: providerName,
      displayName: `${mapping.modelId} (${providerName})`,
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
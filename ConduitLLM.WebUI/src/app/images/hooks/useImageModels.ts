import { useQuery } from '@tanstack/react-query';
import { ProviderType } from '@knn_labs/conduit-admin-client';

// Helper function to convert ProviderType enum to display name
function getProviderDisplayName(providerType: ProviderType): string {
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

export interface ImageModel {
  id: string;
  providerId: string;
  displayName: string;
  maxContextTokens?: number;
  supportsImageGeneration: boolean;
}

export function useImageModels() {
  return useQuery({
    queryKey: ['image-models'],
    queryFn: async () => {
      const response = await fetch('/api/model-mappings');
      if (!response.ok) {
        throw new Error('Failed to fetch model mappings');
      }
      
      interface ModelMapping {
        modelId: string;
        providerId: number;
        providerType?: ProviderType;
        supportsImageGeneration?: boolean;
        maxContextLength?: number;
        isEnabled?: boolean;
      }
      
      const mappings = await response.json() as ModelMapping[];
      
      // Filter for image generation models that are enabled
      const imageModels: ImageModel[] = mappings
        .filter((mapping: ModelMapping) => 
          mapping.supportsImageGeneration === true && mapping.isEnabled !== false
        )
        .map((mapping: ModelMapping) => {
          const providerName = mapping.providerType !== undefined 
            ? getProviderDisplayName(mapping.providerType) 
            : 'Unknown';
          return {
            id: mapping.modelId,
            providerId: mapping.providerType?.toString() ?? 'unknown',
            displayName: `${mapping.modelId} (${providerName})`,
            maxContextTokens: mapping.maxContextLength,
            supportsImageGeneration: true,
          };
        });
      
      return imageModels.sort((a, b) => {
        if (a.providerId !== b.providerId) {
          return a.providerId.localeCompare(b.providerId);
        }
        return a.id.localeCompare(b.id);
      });
    },
    staleTime: 5 * 60 * 1000,
  });
}
import { useQuery } from '@tanstack/react-query';
import { ModelWithCapabilities } from '../types';
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

export function useModels() {
  return useQuery({
    queryKey: ['chat-models'],
    queryFn: async () => {
      // Fetch model mappings through the WebUI API route that uses the SDK
      const response = await fetch('/api/model-mappings');
      if (!response.ok) {
        throw new Error('Failed to fetch model mappings');
      }
      
      interface ModelMapping {
        modelId: string;
        providerId: string;
        providerType?: ProviderType;
        maxContextLength?: number;
        supportsVision?: boolean;
        supportsChat?: boolean;
      }
      
      const mappings = await response.json() as ModelMapping[];
      
      // Filter to only include chat-capable models
      const chatModels = mappings.filter((mapping: ModelMapping) => mapping.supportsChat === true);
      
      const models: ModelWithCapabilities[] = chatModels.map((mapping: ModelMapping) => {
        console.warn('[useModels] Chat model mapping:', mapping);
        console.warn('[useModels] providerType:', mapping.providerType, 'type:', typeof mapping.providerType);
        const providerName = mapping.providerType !== undefined ? getProviderName(mapping.providerType) : 'Unknown';
        console.warn('[useModels] Provider name result:', providerName);
        return {
          id: mapping.modelId,
          providerId: mapping.providerType?.toString() ?? 'unknown',
          providerName: providerName,
          displayName: `${mapping.modelId} (${providerName})`,
          maxContextTokens: mapping.maxContextLength,
          supportsVision: mapping.supportsVision ?? false,
          // Function calling support will need to be detected at runtime
          // since it's not stored in the database
          supportsFunctionCalling: false,
          supportsToolUsage: false,
          supportsJsonMode: false,
          supportsStreaming: true,
        };
      });
      
      return models.sort((a, b) => {
        if (a.providerId !== b.providerId) {
          return a.providerId.localeCompare(b.providerId);
        }
        return a.id.localeCompare(b.id);
      });
    },
    staleTime: 5 * 60 * 1000,
  });
}
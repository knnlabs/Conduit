import { useQuery } from '@tanstack/react-query';
import { ProviderType } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';

// Helper function to convert ProviderType enum to display name
function getProviderDisplayName(providerType: ProviderType): string {
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

export interface ImageModel {
  id: string;
  providerId: string;
  providerName?: string;
  displayName: string;
  maxContextTokens?: number;
  supportsImageGeneration: boolean;
}

export function useImageModels() {
  return useQuery({
    queryKey: ['image-models'],
    queryFn: async () => {
      const result = await withAdminClient(client => 
        client.modelMappings.list()
      );
      
      const mappings = result;
      
      // Filter for image generation models that are enabled
      const imageModels: ImageModel[] = mappings
        .filter(mapping => 
          mapping.supportsImageGeneration === true && mapping.isEnabled !== false
        )
        .map(mapping => {
          const providerDisplayName = mapping.provider?.displayName ?? 
            (mapping.providerType !== undefined 
              ? getProviderDisplayName(mapping.providerType) 
              : 'Unknown');
          return {
            id: mapping.modelId,
            providerId: mapping.providerType?.toString() ?? 'unknown',
            providerName: providerDisplayName,
            displayName: `${mapping.modelId} (${providerDisplayName})`,
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
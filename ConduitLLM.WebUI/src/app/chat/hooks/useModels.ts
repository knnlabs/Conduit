import { useQuery } from '@tanstack/react-query';
import { ModelWithCapabilities } from '../types';
import { ProviderType } from '@knn_labs/conduit-admin-client';

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
        provider?: {
          id: number;
          providerType: ProviderType;
          displayName: string;
          isEnabled: boolean;
        };
        maxContextLength?: number;
        supportsVision?: boolean;
        supportsChat?: boolean;
      }
      
      const mappings = await response.json() as ModelMapping[];
      
      // Filter to only include chat-capable models
      const chatModels = mappings.filter((mapping: ModelMapping) => mapping.supportsChat === true);
      
      const models: ModelWithCapabilities[] = chatModels.map((mapping: ModelMapping) => {
        const providerName = mapping.provider?.displayName ?? 'unknown';
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
import { useQuery } from '@tanstack/react-query';
import { ModelWithCapabilities } from '../types';

export function useModels() {
  return useQuery({
    queryKey: ['chat-models'],
    queryFn: async () => {
      // Fetch model mappings through the WebUI API route that uses the SDK
      const response = await fetch('/api/model-mappings');
      if (!response.ok) {
        throw new Error('Failed to fetch model mappings');
      }
      
      const mappings = await response.json();
      
      const models: ModelWithCapabilities[] = mappings.map((mapping: any) => {
        return {
          id: mapping.modelId,
          providerId: mapping.providerId,
          displayName: `${mapping.modelId} (${mapping.providerId})`,
          maxContextTokens: mapping.maxContextTokens,
          supportsVision: mapping.supportsVision || false,
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
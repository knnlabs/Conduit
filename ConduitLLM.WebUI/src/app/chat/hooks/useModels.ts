import { useQuery } from '@tanstack/react-query';
import { ModelWithCapabilities } from '../types';
import { withAdminClient } from '@/lib/client/adminClient';

export function useModels() {
  return useQuery({
    queryKey: ['chat-models'],
    queryFn: async () => {
      // Fetch model mappings through the Admin SDK
      const result = await withAdminClient(client => 
        client.modelMappings.list()
      );
      
      const mappings = result.map(mapping => ({
        ...mapping,
        providerId: mapping.providerId.toString(),
      }));
      
      // Filter to only include chat-capable models
      const chatModels = mappings.filter(mapping => mapping.supportsChat === true);
      
      const models: ModelWithCapabilities[] = chatModels.map(mapping => {
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
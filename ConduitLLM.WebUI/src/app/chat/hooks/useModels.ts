import { useQuery } from '@tanstack/react-query';
import { ModelWithCapabilities } from '../types';
import { withAdminClient } from '@/lib/client/adminClient';

export function useModels() {
  return useQuery({
    queryKey: ['chat-models'],
    queryFn: async () => {
      // Fetch models through the Admin SDK
      const models = await withAdminClient(client => 
        client.models.list()
      );
      
      // Filter to only include chat-capable models (modelType === 0 is Text/Chat)
      const chatModels = models.filter(model => model.modelType === 0);
      
      // Map to the expected format
      // Note: We're using simplified data since the new schema doesn't have all the old properties
      const mappedModels: ModelWithCapabilities[] = chatModels.map(model => {
        return {
          id: model.id?.toString() ?? 'unknown',
          providerId: 'unknown', // Provider info is on ModelProviderMapping, not Model
          providerName: 'Model Provider',
          displayName: model.name ?? 'Unnamed Model',
          maxContextTokens: model.capabilities?.maxTokens ?? 128000,
          supportsVision: model.capabilities?.supportsVision ?? false,
          supportsFunctionCalling: model.capabilities?.supportsFunctionCalling ?? false,
          supportsToolUsage: false, // Not in the new schema
          supportsJsonMode: false, // Not in the new schema
          supportsStreaming: model.capabilities?.supportsStreaming ?? true,
        };
      });
      
      return mappedModels.sort((a, b) => {
        // Sort by display name
        return a.displayName.localeCompare(b.displayName);
      });
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}
import { useQuery } from '@tanstack/react-query';
import { ModelWithCapabilities } from '../types';
import { withAdminClient } from '@/lib/client/adminClient';

export function useModels() {
  return useQuery({
    queryKey: ['chat-models'],
    queryFn: async () => {
      // Fetch model mappings and models in parallel
      const [mappings, models, providersResponse] = await Promise.all([
        withAdminClient(client => client.modelMappings.list()),
        withAdminClient(client => client.models.list()),
        withAdminClient(client => client.providers.list(1, 100)) // Get up to 100 providers
      ]);
      
      // Create lookup maps for efficient access
      const modelsMap = new Map(models.map(m => [m.id, m]));
      const providersMap = new Map(providersResponse.items.map(p => [p.id, p]));
      
      // Filter mappings to only include enabled chat-capable models
      const chatMappings = mappings.filter(mapping => {
        // Must be enabled
        if (!mapping.isEnabled) return false;
        
        // Get the associated model
        const model = modelsMap.get(mapping.modelId);
        if (!model) return false;
        
        // Must be a text/chat model (modelType === 0)
        if (model.modelType !== 0) return false;
        
        // Must support chat capability
        if (!model.capabilities?.supportsChat) return false;
        
        return true;
      });
      
      // Map to the expected format
      const mappedModels: ModelWithCapabilities[] = chatMappings.map(mapping => {
        const model = modelsMap.get(mapping.modelId);
        const provider = providersMap.get(mapping.providerId);
        
        return {
          id: mapping.modelAlias, // Use the alias as the ID for API calls
          providerId: mapping.providerId.toString(),
          providerName: provider?.providerName ?? 'Unknown Provider',
          displayName: mapping.modelAlias, // Use alias as display name
          maxContextTokens: mapping.maxContextTokensOverride ?? model?.capabilities?.maxTokens ?? 128000,
          supportsVision: model?.capabilities?.supportsVision ?? false,
          supportsFunctionCalling: model?.capabilities?.supportsFunctionCalling ?? false,
          supportsToolUsage: false, // Not in the new schema
          supportsJsonMode: false, // Not in the new schema
          supportsStreaming: model?.capabilities?.supportsStreaming ?? true,
        };
      });
      
      // Sort by display name and remove duplicates (in case of multiple mappings for same alias)
      const uniqueModels = Array.from(
        new Map(mappedModels.map(m => [m.id, m])).values()
      );
      
      return uniqueModels.sort((a, b) => {
        // Sort by display name
        return a.displayName.localeCompare(b.displayName);
      });
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}
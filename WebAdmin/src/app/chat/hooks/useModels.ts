import { useQuery } from '@tanstack/react-query';
import { ModelWithCapabilities } from '../types';
import { withAdminClient } from '@/lib/client/adminClient';

export function useModels() {
  return useQuery({
    queryKey: ['chat-models'],
    queryFn: async () => {
      // Fetch model mappings, providers, and model provider associations
      const [mappings, providersResponse] = await Promise.all([
        withAdminClient(client => client.modelMappings.list()),
        withAdminClient(client => client.providers.list(1, 100)) // Get up to 100 providers
      ]);
      
      // Create lookup map for providers
      const providersMap = new Map(providersResponse.items.map(p => [p.id, p]));
      
      // For now, return all enabled mappings as chat models
      // We can't filter by capability without the Model data
      // This needs a proper backend endpoint that returns enriched data
      const chatMappings = mappings.filter(mapping => {
        // Must be enabled
        return mapping.isEnabled;
      });
      
      // Map to the expected format
      const mappedModels: ModelWithCapabilities[] = chatMappings.map(mapping => {
        const provider = providersMap.get(mapping.providerId);
        
        return {
          id: mapping.modelAlias, // Use the alias as the ID for API calls
          providerId: mapping.providerId.toString(),
          providerName: provider?.providerName ?? 'Unknown Provider',
          displayName: mapping.modelAlias, // Use alias as display name
          maxContextTokens: 128000, // Default since we don't have model data
          supportsVision: false, // We don't have this data without the Model
          supportsFunctionCalling: false, // We don't have this data
          supportsToolUsage: false, // Not in the new schema
          supportsJsonMode: false, // Not in the new schema
          supportsStreaming: true, // Default to true
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
    staleTime: 30 * 1000, // 30 seconds - short cache to quickly reflect model mapping changes
  });
}
import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';

export interface ImageModel {
  id: string;
  providerId: string;
  providerName: string;
  displayName: string;
  maxContextTokens?: number | null;
  supportsImageGeneration: boolean;
}

export function useImageModels() {
  return useQuery({
    queryKey: ['image-models'],
    queryFn: async () => {
      // Fetch model mappings and providers
      const [mappings, providersResponse] = await Promise.all([
        withAdminClient(client => client.modelMappings.list()),
        withAdminClient(client => client.providers.list(1, 100)) // Get up to 100 providers
      ]);
      
      // Create lookup map for providers
      const providersMap = new Map(providersResponse.items.map(p => [p.id, p]));
      
      // For now, return all enabled mappings as image models
      // We can't filter by capability without the Model data
      // TODO: Backend should provide enriched mappings with model capabilities
      const imageMappings = mappings.filter(mapping => {
        // Must be enabled
        return mapping.isEnabled;
      });
      
      // Map to the expected format
      const imageModels: ImageModel[] = imageMappings.map(mapping => {
        const provider = providersMap.get(mapping.providerId);
        
        return {
          id: mapping.modelAlias, // Use the alias as the ID for API calls
          providerId: mapping.providerId.toString(),
          providerName: provider?.providerName ?? 'Unknown Provider',
          displayName: mapping.modelAlias, // Use alias as display name
          maxContextTokens: null, // No override data available
          supportsImageGeneration: true, // Assume true for now
        };
      });
      
      // Sort by provider name and then by display name, removing duplicates
      const uniqueModels = Array.from(
        new Map(imageModels.map(m => [m.id, m])).values()
      );
      
      return uniqueModels.sort((a, b) => {
        if (a.providerName !== b.providerName) {
          return a.providerName.localeCompare(b.providerName);
        }
        return a.displayName.localeCompare(b.displayName);
      });
    },
    staleTime: 5 * 60 * 1000,
    retry: 3,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}
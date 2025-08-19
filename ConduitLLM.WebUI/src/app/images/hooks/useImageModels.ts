import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';

export interface ImageModel {
  id: string;
  providerId: string;
  providerName: string;
  displayName: string;
  maxContextTokens?: number;
  supportsImageGeneration: boolean;
}

export function useImageModels() {
  return useQuery({
    queryKey: ['image-models'],
    queryFn: async () => {
      // Fetch model mappings, models, and providers in parallel
      const [mappings, models, providersResponse] = await Promise.all([
        withAdminClient(client => client.modelMappings.list()),
        withAdminClient(client => client.models.list()),
        withAdminClient(client => client.providers.list(1, 100)) // Get up to 100 providers
      ]);
      
      // Create lookup maps for efficient access
      const modelsMap = new Map(models.map(m => [m.id, m]));
      const providersMap = new Map(providersResponse.items.map(p => [p.id, p]));
      
      // Filter mappings to only include enabled image generation models
      const imageMappings = mappings.filter(mapping => {
        // Must be enabled
        if (!mapping.isEnabled) return false;
        
        // Get the associated model
        const model = modelsMap.get(mapping.modelId);
        if (!model) return false;
        
        // Must be an image model (modelType === 1)
        if (model.modelType !== 1) return false;
        
        // Must support image generation capability
        if (!model.capabilities?.supportsImageGeneration) return false;
        
        // Must be active
        if (model.isActive === false) return false;
        
        return true;
      });
      
      // Map to the expected format
      const imageModels: ImageModel[] = imageMappings.map(mapping => {
        const model = modelsMap.get(mapping.modelId);
        const provider = providersMap.get(mapping.providerId);
        
        return {
          id: mapping.modelAlias, // Use the alias as the ID for API calls
          providerId: mapping.providerId.toString(),
          providerName: provider?.providerName ?? 'Unknown Provider',
          displayName: mapping.modelAlias, // Use alias as display name
          maxContextTokens: mapping.maxContextTokensOverride ?? model?.capabilities?.maxTokens,
          supportsImageGeneration: true,
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
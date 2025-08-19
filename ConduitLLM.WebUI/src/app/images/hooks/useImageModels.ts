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
      // Fetch models through the Admin SDK
      const models = await withAdminClient(client => 
        client.models.list()
      );
      
      // Filter for image generation models (modelType === 1)
      const imageModels: ImageModel[] = models
        .filter(model => 
          model.modelType === 1 && model.isActive !== false
        )
        .map(model => {
          return {
            id: model.id?.toString() ?? 'unknown',
            providerId: 'unknown', // Provider info is on ModelProviderMapping
            providerName: 'Image Provider',
            displayName: model.name ?? 'Unnamed Model',
            maxContextTokens: model.capabilities?.maxTokens,
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
    retry: 3,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}
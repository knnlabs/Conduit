import { useQuery } from '@tanstack/react-query';

export interface ImageModel {
  id: string;
  providerId: string;
  displayName: string;
  maxContextTokens?: number;
  supportsImageGeneration: boolean;
}

export function useImageModels() {
  return useQuery({
    queryKey: ['image-models'],
    queryFn: async () => {
      const response = await fetch('/api/model-mappings');
      if (!response.ok) {
        throw new Error('Failed to fetch model mappings');
      }
      
      const mappings = await response.json();
      
      // Filter for image generation models only
      const imageModels: ImageModel[] = mappings
        .filter((mapping: any) => mapping.supportsImageGeneration)
        .map((mapping: any) => ({
          id: mapping.modelId,
          providerId: mapping.providerId,
          displayName: `${mapping.modelId} (${mapping.providerId})`,
          maxContextTokens: mapping.maxContextLength,
          supportsImageGeneration: mapping.supportsImageGeneration,
        }));
      
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
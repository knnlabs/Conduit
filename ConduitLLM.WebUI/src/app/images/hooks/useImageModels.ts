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
      
      interface ModelMapping {
        modelId: string;
        providerId: string;
        supportsImageGeneration: boolean;
        maxContextLength?: number;
      }
      
      const mappings = await response.json() as ModelMapping[];
      
      // Filter for image generation models only
      const imageModels: ImageModel[] = mappings
        .filter((mapping: ModelMapping) => mapping.supportsImageGeneration)
        .map((mapping: ModelMapping) => ({
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
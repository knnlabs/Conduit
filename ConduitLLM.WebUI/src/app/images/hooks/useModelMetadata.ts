import { useQuery } from '@tanstack/react-query';

export interface ImageMetadata {
  sizes?: string[];
  aspectRatios?: string[];
  maxImages?: number;
  qualityOptions?: string[];
  styleOptions?: string[];
  responseFormats?: string[];
  defaultSize?: string;
  defaultQuality?: string;
  defaultStyle?: string;
}

export interface VideoMetadata {
  resolutions?: string[];
  aspectRatios?: string[];
  maxDurationSeconds?: number;
  fps?: number[];
  qualityOptions?: string[];
  formats?: string[];
  defaultResolution?: string;
}

export interface ModelMetadata {
  modelId: string;
  metadata?: {
    image?: ImageMetadata;
    video?: VideoMetadata;
  };
}

export function useModelMetadata(modelId: string | null) {
  return useQuery({
    queryKey: ['model-metadata', modelId],
    queryFn: async () => {
      if (!modelId) return null;
      
      const response = await fetch(`/api/models/${modelId}/metadata`);
      if (!response.ok) {
        if (response.status === 404) {
          // No metadata found, return null
          return null;
        }
        throw new Error('Failed to fetch model metadata');
      }
      
      return response.json() as Promise<ModelMetadata>;
    },
    enabled: !!modelId,
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  });
}
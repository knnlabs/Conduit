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
      
      try {
        // Note: getMetadata method doesn't exist in Admin SDK
        // This is a placeholder implementation that returns null
        // TODO: Implement metadata retrieval once SDK supports it
        return Promise.resolve(null);
      } catch (error) {
        // If metadata not found, return null
        if (error instanceof Error && error.message.includes('404')) {
          return null;
        }
        throw error;
      }
    },
    enabled: !!modelId,
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  });
}
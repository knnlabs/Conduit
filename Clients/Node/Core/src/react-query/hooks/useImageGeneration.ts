import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduit } from '../ConduitProvider';
import type { ImageGenerationRequest, ImageGenerationResponse } from '../../models/images';

export interface UseImageGenerationOptions 
  extends Omit<
    UseMutationOptions<ImageGenerationResponse, Error, ImageGenerationRequest>,
    'mutationFn'
  > {}

export function useImageGeneration(options?: UseImageGenerationOptions) {
  const { client } = useConduit();

  return useMutation({
    mutationFn: async (request: ImageGenerationRequest) => {
      return await client.images.generate(request);
    },
    ...options,
  });
}
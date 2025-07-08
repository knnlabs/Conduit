import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduit } from '../ConduitProvider';
import type { AsyncVideoGenerationRequest, AsyncVideoGenerationResponse } from '../../models/videos';

export interface UseVideoGenerationOptions 
  extends Omit<
    UseMutationOptions<AsyncVideoGenerationResponse, Error, AsyncVideoGenerationRequest>,
    'mutationFn'
  > {}

export function useVideoGeneration(options?: UseVideoGenerationOptions) {
  const { client } = useConduit();

  return useMutation({
    mutationFn: async (request: AsyncVideoGenerationRequest) => {
      return await client.videos.generateAsync(request);
    },
    ...options,
  });
}
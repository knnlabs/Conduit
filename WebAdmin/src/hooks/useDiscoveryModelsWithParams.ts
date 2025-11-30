import { useQuery } from '@tanstack/react-query';
import { getBrowserCoreClient } from '@/lib/client/browserCoreClient';
import type { DiscoveredModel as SDKDiscoveredModel } from '@knn_labs/conduit-gateway-client';

// Extend the SDK type to include backend fields not in the generated types
export interface DiscoveryModel extends SDKDiscoveredModel {
  parameters?: string;
  max_tokens?: number;
  max_output_tokens?: number;
}

export interface DiscoveryResponse {
  data: DiscoveryModel[];
  count: number;
}

/**
 * Enhanced hook that fetches discovery models and their parameters in parallel
 * @param capability - Optional capability filter (e.g., "chat", "image_generation", "video_generation")
 * @returns Query result with models and their parameters
 */
export function useDiscoveryModelsWithParams(capability?: string) {
  return useQuery<DiscoveryResponse>({
    queryKey: ['discovery-models-with-params', capability],
    queryFn: async () => {
      try {
        // Get the browser client with ephemeral key
        const client = await getBrowserCoreClient();
        
        // Use the SDK directly - let the backend handle filtering
        const modelsResponse = capability 
          ? await client.discovery.getModelsByCapability(capability)
          : await client.discovery.getModels();

        // Parameters are already included in the discovery response from backend
        // The backend includes the parameters field for each model
        return modelsResponse as DiscoveryResponse;
      } catch (error) {
        console.error('Failed to fetch discovery models:', error);
        throw error;
      }
    },
    staleTime: 30 * 1000, // 30 seconds - short cache to quickly reflect model mapping changes
    retry: 2,
  });
}
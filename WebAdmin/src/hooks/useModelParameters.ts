import { useQuery } from '@tanstack/react-query';
import { getBrowserCoreClient } from '@/lib/client/browserCoreClient';

interface ModelParametersResponse {
  model_id: number;
  model_alias: string;
  series_name: string;
  parameters: Record<string, unknown>;
}

/**
 * Hook to fetch model parameters from the Core API discovery endpoint
 * @param modelAlias - The model alias or ID to fetch parameters for
 * @returns Query result with model parameters
 */
export function useModelParameters(modelAlias: string | null) {
  return useQuery<ModelParametersResponse | null>({
    queryKey: ['model-parameters', modelAlias],
    queryFn: async () => {
      if (!modelAlias) {
        return null;
      }

      try {
        // Get the browser client with ephemeral key
        const client = await getBrowserCoreClient();
        
        // Use the SDK directly
        const data = await client.discovery.getModelParameters(modelAlias);
        return data;
      } catch (error) {
        console.warn(`Failed to fetch parameters for model ${modelAlias}:`, error);
        return null;
      }
    },
    enabled: !!modelAlias,
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
    retry: 1, // Only retry once on failure
  });
}

/**
 * Helper function to extract parameters JSON string from the response
 * @param data - The model parameters response
 * @returns JSON string of parameters or empty object string
 */
export function extractParametersJson(data: ModelParametersResponse | null | undefined): string {
  if (!data?.parameters) {
    return '{}';
  }
  
  try {
    return JSON.stringify(data.parameters);
  } catch {
    return '{}';
  }
}
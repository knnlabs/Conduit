import { useQuery } from '@tanstack/react-query';

export interface DiscoveryModel {
  id: string;
  provider: string;
  display_name: string;
  capabilities: Record<string, unknown>;
  parameters?: string;
  // Add all capability flags
  supports_chat?: boolean;
  supports_streaming?: boolean;
  supports_vision?: boolean;
  supports_function_calling?: boolean;
  supports_audio_transcription?: boolean;
  supports_text_to_speech?: boolean;
  supports_realtime_audio?: boolean;
  supports_video_generation?: boolean;
  supports_image_generation?: boolean;
  supports_embeddings?: boolean;
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
        // First fetch the models via the Next.js API route
        const modelsUrl = capability 
          ? `/api/discovery/models?capability=${capability}`
          : '/api/discovery/models';
        
        const res = await fetch(modelsUrl);
        
        if (!res.ok) {
          throw new Error(`Failed to fetch discovery models: ${res.statusText}`);
        }

        const modelsResponse = await res.json() as DiscoveryResponse;

        // Then fetch parameters for each model in parallel
        const modelsWithParams = await Promise.all(
          modelsResponse.data.map(async (model) => {
            try {
              const paramRes = await fetch(
                `/api/discovery/models/${encodeURIComponent(model.id)}/parameters`
              );

              if (!paramRes.ok) {
                // If parameters not found, return empty
                if (paramRes.status === 404) {
                  return {
                    ...model,
                    parameters: '{}',
                  };
                }
                console.warn(`Failed to fetch parameters for model ${model.id}`);
                return {
                  ...model,
                  parameters: '{}',
                };
              }

              const data = await paramRes.json() as { parameters?: Record<string, unknown> };
              const paramsString = data.parameters ? JSON.stringify(data.parameters) : '{}';
              
              return {
                ...model,
                parameters: paramsString,
              };
            } catch (error) {
              console.warn(`Failed to fetch parameters for model ${model.id}:`, error);
              return {
                ...model,
                parameters: '{}',
              };
            }
          })
        );

        return {
          data: modelsWithParams,
          count: modelsWithParams.length,
        };
      } catch (error) {
        console.error('Failed to fetch discovery models:', error);
        throw error;
      }
    },
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
    retry: 2,
  });
}
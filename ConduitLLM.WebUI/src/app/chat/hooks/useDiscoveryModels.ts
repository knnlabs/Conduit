import { useQuery } from '@tanstack/react-query';

export interface DiscoveryModel {
  id: string;
  provider: string;
  display_name: string;
  capabilities: Record<string, unknown>;
  parameters?: string | null;
}

export interface DiscoveryResponse {
  data: DiscoveryModel[];
  count: number;
}

export function useDiscoveryModels(capability?: string) {
  return useQuery<DiscoveryResponse>({
    queryKey: ['discovery-models', capability],
    queryFn: async () => {
      // Use fetch directly for discovery endpoint
      const response = await fetch(`/api/discovery/models${capability ? `?capability=${capability}` : ''}`);
      if (!response.ok) {
        throw new Error('Failed to fetch discovery models');
      }
      const data = await response.json() as DiscoveryResponse;
      return data;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}
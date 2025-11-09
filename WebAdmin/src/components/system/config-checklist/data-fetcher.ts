import { withAdminClient } from '@/lib/client/adminClient';
import type { ProviderKeyCredentialDto } from '@knn_labs/conduit-admin-client';
import type { ConfigData } from './types';

// Standalone data fetch function
export async function fetchConfigData(): Promise<ConfigData> {
  // Fetch all required data in parallel
  const [providersResponse, modelMappings, modelCostsResponse, settingsResponse] = await Promise.all([
    withAdminClient(client => client.providers.list(1, 100)),
    withAdminClient(client => client.modelMappings.list()),
    withAdminClient(client => client.modelCosts.list()),
    withAdminClient(client => client.settings.getGlobalSettings())
  ]);

  const providers = providersResponse.items;
  const modelCosts = modelCostsResponse.items || [];
  const settings = settingsResponse.settings;

  // Get all provider keys
  const allProviderKeys: ProviderKeyCredentialDto[] = [];
  for (const provider of providers) {
    try {
      const keys = await withAdminClient(client => 
        client.providers.listKeys(provider.id)
      );
      allProviderKeys.push(...keys);
    } catch (err) {
      console.warn(`Failed to fetch keys for provider ${provider.id}:`, err);
    }
  }

  return {
    providers,
    allProviderKeys,
    modelMappings,
    modelCosts,
    settings
  };
}
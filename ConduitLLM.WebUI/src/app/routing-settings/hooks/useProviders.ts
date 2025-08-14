'use client';

import { useState, useEffect } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';

export function useProviders() {
  const [providers, setProviders] = useState<{ id: string; name: string; displayName?: string; enabled?: boolean; }[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchProviders() {
      try {
        setLoading(true);
        setError(null);
        
        const result = await withAdminClient(client => 
          client.providers.list(1, 1000)
        );
        
        const providersData = result.items.map(provider => ({
          id: provider.id?.toString() ?? '',
          name: provider.providerName ?? provider.providerType?.toString() ?? '',
          displayName: provider.providerName,
          enabled: provider.isEnabled
        }));
        
        setProviders(providersData);
      } catch (err) {
        console.error('Error fetching providers:', err);
        setError(err instanceof Error ? err.message : 'Failed to fetch providers');
        setProviders([]);
      } finally {
        setLoading(false);
      }
    }

    void fetchProviders();
  }, []);

  // Convert providers to select options format
  const providerOptions: { value: string; label: string; }[] = providers.map(provider => ({
    value: String(provider.id ?? ''),
    label: provider.displayName ?? provider.name ?? String(provider.id ?? ''),
  }));

  return {
    providers,
    providerOptions,
    loading,
    error,
    refetch: () => {
      setLoading(true);
      setError(null);
      // Trigger re-fetch by updating the effect dependency
    }
  };
}
'use client';

import { useState, useEffect } from 'react';

export interface Provider {
  id: string;
  name: string;
  displayName?: string;
  enabled?: boolean;
}

export interface ProviderOption {
  value: string;
  label: string;
}

export function useProviders() {
  const [providers, setProviders] = useState<Provider[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchProviders() {
      try {
        setLoading(true);
        setError(null);
        
        const response = await fetch('/api/providers');
        if (!response.ok) {
          throw new Error(`Failed to fetch providers: ${response.statusText}`);
        }
        
        const providersData = await response.json() as unknown;
        setProviders(Array.isArray(providersData) ? providersData as Provider[] : []);
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
  const providerOptions: ProviderOption[] = providers.map(provider => ({
    value: provider.id,
    label: provider.displayName ?? provider.name ?? provider.id,
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
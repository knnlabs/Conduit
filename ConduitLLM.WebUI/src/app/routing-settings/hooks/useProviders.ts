'use client';

import { useState, useEffect } from 'react';


export function useProviders() {
  const [providers, setProviders] = useState<{ id: string; name: string; displayName?: string; enabled?: boolean; }[]>([]);
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
        setProviders(Array.isArray(providersData) ? providersData as { id: string; name: string; displayName?: string; enabled?: boolean; }[] : []);
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
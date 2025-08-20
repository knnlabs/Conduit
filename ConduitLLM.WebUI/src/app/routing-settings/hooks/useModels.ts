'use client';

import { useState, useEffect } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';
import type { DiscoveredModel } from '../types/models';



export function useModels() {
  const [models, setModels] = useState<DiscoveredModel[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchModels() {
      try {
        setLoading(true);
        setError(null);
        
        // Fetch models from the catalog instead of discovery
        const catalogModels = await withAdminClient(client => 
          client.models.list()
        );
        
        // Transform catalog models to DiscoveredModel format
        const modelsArray: DiscoveredModel[] = catalogModels.map(model => ({
          id: model.name ?? model.id?.toString() ?? '',
          displayName: model.name ?? 'Unnamed Model',
          providerId: '1', // Provider info is on mappings, not models
          supportsVision: Boolean(model.capabilities?.supportsVision),
          supportsFunctionCalling: Boolean(model.capabilities?.supportsFunctionCalling),
          supportsStreaming: Boolean(model.capabilities?.supportsStreaming ?? true),
          capabilities: []
        }));
        setModels(modelsArray);
      } catch (err) {
        console.error('Error fetching models:', err);
        setError(err instanceof Error ? err.message : 'Failed to fetch models');
        setModels([]);
      } finally {
        setLoading(false);
      }
    }

    void fetchModels();
  }, []);

  // Convert models to select options format
  const modelOptions: { value: string; label: string; }[] = models.map((model) => ({
    value: model.id,
    label: model.displayName ?? model.id,
  }));

  return {
    models,
    modelOptions,
    loading,
    error,
  };
}
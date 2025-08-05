'use client';

import { useState, useEffect } from 'react';
import type { DiscoveredModel, ModelsDiscoveryResponse } from '../types/models';



export function useModels() {
  const [models, setModels] = useState<DiscoveredModel[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchModels() {
      try {
        setLoading(true);
        setError(null);
        
        const response = await fetch('/api/discovery/models');
        if (!response.ok) {
          throw new Error(`Failed to fetch models: ${response.statusText}`);
        }
        
        const modelsData = await response.json() as ModelsDiscoveryResponse;
        
        // Extract models array from response
        const modelsArray = modelsData.models ?? [];
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
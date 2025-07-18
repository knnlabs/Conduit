'use client';

import { useState, useEffect } from 'react';

export interface Model {
  id: string;
  name: string;
  provider: string;
  capabilities: Record<string, boolean>;
}

export interface ModelOption {
  value: string;
  label: string;
}

export function useModels() {
  const [models, setModels] = useState<Model[]>([]);
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
        
        const modelsData = await response.json();
        
        // Extract models array from response
        const modelsArray = modelsData.data ?? modelsData.items ?? modelsData;
        setModels(Array.isArray(modelsArray) ? modelsArray : []);
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
  const modelOptions: ModelOption[] = models.map(model => ({
    value: model.id,
    label: model.name ?? model.id,
  }));

  return {
    models,
    modelOptions,
    loading,
    error,
  };
}
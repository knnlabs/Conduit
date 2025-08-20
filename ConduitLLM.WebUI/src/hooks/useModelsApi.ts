'use client';

import { useState, useEffect } from 'react';
import { useAdminClient } from '@/lib/client/adminClient';
import type { ModelDto } from '@knn_labs/conduit-admin-client';

export function useModels() {
  const [models, setModels] = useState<ModelDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { executeWithAdmin } = useAdminClient();

  useEffect(() => {
    void fetchModels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fetchModels = async () => {
    try {
      setIsLoading(true);
      setError(null);
      
      const data = await executeWithAdmin(client => client.models.list());
      setModels(data);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch models';
      setError(errorMessage);
      console.error('Error fetching models:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const getModelById = (id: number): ModelDto | undefined => {
    return models.find(m => m.id === id);
  };

  const getModelsByType = (type: number): ModelDto[] => {
    return models.filter(m => m.modelType === type);
  };

  return {
    models,
    isLoading,
    error,
    refetch: fetchModels,
    getModelById,
    getModelsByType
  };
}
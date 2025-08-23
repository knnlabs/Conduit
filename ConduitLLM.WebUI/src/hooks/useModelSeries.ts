import { useState, useEffect } from 'react';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';
import type { ModelDto } from '@knn_labs/conduit-admin-client';

/**
 * Hook to fetch and cache model series names
 * Prevents duplicate API calls across components
 */
export function useModelSeries(models: ModelDto[]) {
  const [seriesNames, setSeriesNames] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const { executeWithAdmin } = useAdminClient();

  useEffect(() => {
    const loadSeriesNames = async () => {
      // Extract unique series IDs from models
      const uniqueSeriesIds = Array.from(
        new Set(
          models
            .map(model => model.modelSeriesId)
            .filter((id): id is number => id !== undefined && id !== null)
        )
      );

      if (uniqueSeriesIds.length === 0) {
        setSeriesNames({});
        return;
      }

      setLoading(true);
      setError(null);

      try {
        const names: Record<number, string> = {};
        
        // Fetch all series names in parallel
        const results = await Promise.allSettled(
          uniqueSeriesIds.map(async (seriesId) => {
            const series = await executeWithAdmin(client => 
              client.modelSeries.get(seriesId)
            );
            return { id: seriesId, name: series.name ?? `Series ${seriesId}` };
          })
        );

        // Process results, handling both successes and failures
        results.forEach((result) => {
          if (result.status === 'fulfilled') {
            names[result.value.id] = result.value.name;
          } else {
            // Log error but don't fail the entire operation
            console.warn(`Failed to load series name:`, result.reason);
          }
        });
        
        setSeriesNames(names);
      } catch (err) {
        const error = err instanceof Error ? err : new Error('Failed to load series names');
        setError(error);
        
        // Show user-friendly error notification
        notifications.show({
          title: 'Warning',
          message: 'Some model series names could not be loaded',
          color: 'yellow',
        });
      } finally {
        setLoading(false);
      }
    };

    if (models.length > 0) {
      void loadSeriesNames();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [models]);

  return { seriesNames, loading, error };
}

/**
 * Hook to fetch a single model series name
 */
export function useModelSeriesById(seriesId: number | null | undefined) {
  const [seriesName, setSeriesName] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const { executeWithAdmin } = useAdminClient();

  useEffect(() => {
    if (!seriesId) {
      setSeriesName(null);
      return;
    }

    const loadSeriesName = async () => {
      setLoading(true);
      setError(null);

      try {
        const series = await executeWithAdmin(client => 
          client.modelSeries.get(seriesId)
        );
        setSeriesName(series.name ?? `Series ${seriesId}`);
      } catch (err) {
        const error = err instanceof Error ? err : new Error('Failed to load series name');
        setError(error);
        setSeriesName(`Series ${seriesId}`);
        
        console.warn(`Failed to load series ${seriesId}:`, error.message);
      } finally {
        setLoading(false);
      }
    };

    void loadSeriesName();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seriesId]);

  return { seriesName, loading, error };
}
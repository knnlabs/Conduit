import { useState, useEffect, useCallback } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';
import { OverallMediaStorageStats } from '../types';

export function useMediaStats() {
  const [stats, setStats] = useState<OverallMediaStorageStats | null>(null);
  const [providerStats, setProviderStats] = useState<Record<string, number>>({});
  const [typeStats, setTypeStats] = useState<Record<string, number>>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchStats = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      // Fetch overall stats using Admin SDK
      const overallData = await withAdminClient(client => 
        client.media.getMediaStats('overall')
      ) as OverallMediaStorageStats;
      setStats(overallData);

      // Fetch provider stats using Admin SDK
      const providerData = await withAdminClient(client => 
        client.media.getMediaStats('by-provider')
      );
      setProviderStats(providerData);

      // Fetch type stats using Admin SDK
      const typeData = await withAdminClient(client => 
        client.media.getMediaStats('by-type')
      );
      setTypeStats(typeData);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error';
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchStats();
  }, [fetchStats]);

  return {
    stats,
    providerStats,
    typeStats,
    loading,
    error,
    refetch: fetchStats,
  };
}
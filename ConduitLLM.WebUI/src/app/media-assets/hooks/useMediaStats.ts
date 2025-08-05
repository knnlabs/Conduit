import { useState, useEffect, useCallback } from 'react';
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
      // Fetch overall stats
      const overallResponse = await fetch('/api/media/stats?type=overall');
      if (!overallResponse.ok) throw new Error('Failed to fetch overall stats');
      const overallData = await overallResponse.json() as OverallMediaStorageStats;
      setStats(overallData);

      // Fetch provider stats
      const providerResponse = await fetch('/api/media/stats?type=by-provider');
      if (!providerResponse.ok) throw new Error('Failed to fetch provider stats');
      const providerData = await providerResponse.json() as Record<string, number>;
      setProviderStats(providerData);

      // Fetch type stats
      const typeResponse = await fetch('/api/media/stats?type=by-type');
      if (!typeResponse.ok) throw new Error('Failed to fetch type stats');
      const typeData = await typeResponse.json() as Record<string, number>;
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
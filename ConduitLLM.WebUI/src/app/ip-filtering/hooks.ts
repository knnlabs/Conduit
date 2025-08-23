import { useState, useCallback } from 'react';
import { useSecurityApi, type IpRule, type IpStats } from '@/hooks/useSecurityApi';
import { notifications } from '@mantine/notifications';

export function useIpFilteringData() {
  const [isLoading, setIsLoading] = useState(true);
  const [rules, setRules] = useState<IpRule[]>([]);
  const [stats, setStats] = useState<IpStats | null>(null);
  const { getIpRules, error } = useSecurityApi();

  const fetchIpRules = useCallback(async () => {
    try {
      setIsLoading(true);
      const fetchedRules = await getIpRules();
      setRules(fetchedRules);
      
      // Calculate statistics from the rules data
      const calculatedStats: IpStats = {
        totalRules: fetchedRules.length,
        allowRules: fetchedRules.filter(r => r.action === 'allow').length,
        blockRules: fetchedRules.filter(r => r.action === 'block').length,
        activeRules: fetchedRules.filter(r => r.isEnabled !== false).length,
        blockedRequests24h: 0, // This would need to come from a real endpoint
        lastRuleUpdate: fetchedRules.length > 0 
          ? new Date(Math.max(...fetchedRules.map(r => new Date(r.createdAt ?? '').getTime()).filter(t => !isNaN(t)))).toISOString()
          : null,
      };
      setStats(calculatedStats);
    } catch {
      notifications.show({
        title: 'Error',
        message: 'Failed to load IP rules',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  }, [getIpRules]);

  return {
    isLoading,
    rules,
    stats,
    error,
    fetchIpRules,
  };
}
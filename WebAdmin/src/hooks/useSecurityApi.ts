'use client';

import { useState, useCallback } from 'react';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import type {
  IpFilterDto,
  CreateIpFilterDto,
  UpdateIpFilterDto,
} from '@/lib/admin-api';

// Legacy interface for backward compatibility - maps to IpFilterDto
export interface IpRule {
  id?: string;
  ipAddress: string;
  action: 'allow' | 'block';
  description?: string;
  createdAt?: string;
  isEnabled?: boolean;
  lastMatchedAt?: string;
  matchCount?: number;
}

// Legacy interface for backward compatibility - maps to IpFilterStatistics
export interface IpStats {
  totalRules: number;
  allowRules: number;
  blockRules: number;
  activeRules: number;
  blockedRequests24h: number;
  lastRuleUpdate: string | null;
}

// Helper functions to convert between legacy and new formats
function ipFilterToLegacyRule(filter: IpFilterDto): IpRule {
  return {
    id: filter.id.toString(),
    ipAddress: filter.ipAddressOrCidr,
    action: filter.filterType === 'whitelist' ? 'allow' : 'block',
    description: filter.description,
    createdAt: filter.createdAt,
    isEnabled: filter.isEnabled,
    // lastMatchedAt/matchCount are no longer tracked server-side (removed from IpFilterDto in #1038)
    lastMatchedAt: undefined,
    matchCount: undefined,
  };
}

function legacyRuleToIpFilter(rule: IpRule): CreateIpFilterDto {
  return {
    name: rule.description ?? `Rule for ${rule.ipAddress}`,
    ipAddressOrCidr: rule.ipAddress,
    filterType: rule.action === 'allow' ? 'whitelist' : 'blacklist',
    isEnabled: rule.isEnabled ?? true,
    description: rule.description,
  };
}


export function useSecurityApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getIpRules = useCallback(async (): Promise<IpRule[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client =>
        client.ipFilters.list()
      );

      // Convert IpFilterDto[] to IpRule[] for backward compatibility
      return result.map(filter => ipFilterToLegacyRule(filter));
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch IP rules';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createIpRule = useCallback(async (rule: IpRule): Promise<IpRule> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const createDto = legacyRuleToIpFilter(rule);
      
      const result = await withAdminClient(client =>
        client.ipFilters.create(createDto)
      );

      notify.success('IP rule created successfully');

      // Convert back to legacy format
      return ipFilterToLegacyRule(result);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create IP rule';
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateIpRule = useCallback(async (id: string, rule: Partial<IpRule>): Promise<IpRule> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const numericId = parseInt(id, 10);
      if (isNaN(numericId)) {
        throw new Error('Invalid rule ID');
      }

      const updateDto: UpdateIpFilterDto = {
        id: numericId,
        name: rule.description,
        ipAddressOrCidr: rule.ipAddress,
        filterType: rule.action === 'allow' ? 'whitelist' : 'blacklist',
        isEnabled: rule.isEnabled,
        description: rule.description,
      };

      await withAdminClient(client =>
        client.ipFilters.update(numericId, updateDto)
      );

      notify.success('IP rule updated successfully');

      // Return the updated rule (we need to fetch it to get the complete data)
      const updatedFilter = await withAdminClient(client =>
        client.ipFilters.getById(numericId)
      );

      return ipFilterToLegacyRule(updatedFilter);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update IP rule';
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const deleteIpRule = useCallback(async (id: string): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const numericId = parseInt(id, 10);
      if (isNaN(numericId)) {
        throw new Error('Invalid rule ID');
      }

      await withAdminClient(client =>
        client.ipFilters.deleteById(numericId)
      );

      notify.success('IP rule deleted successfully');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete IP rule';
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getIpStats = useCallback(async (): Promise<IpStats> => {
    setIsLoading(true);
    setError(null);

    try {
      // The Admin SDK doesn't have a direct stats endpoint, so we'll compute stats from the filters
      const filters = await withAdminClient(client =>
        client.ipFilters.list()
      );

      const stats: IpStats = {
        totalRules: filters.length,
        allowRules: filters.filter(f => f.filterType === 'whitelist').length,
        blockRules: filters.filter(f => f.filterType === 'blacklist').length,
        activeRules: filters.filter(f => f.isEnabled).length,
        // Per-filter match counts are no longer tracked server-side (removed in #1038)
        blockedRequests24h: 0,
        lastRuleUpdate: filters.length > 0 ?
          Math.max(...filters.map(f => new Date(f.updatedAt).getTime())).toString() :
          null,
      };

      return stats;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch IP stats';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getIpRulesForKey = useCallback(async (virtualKeyId: number): Promise<IpRule[]> => {
    setIsLoading(true);
    setError(null);

    try {
      const result = await withAdminClient(client =>
        client.ipFilters.listByVirtualKey(virtualKeyId)
      );
      return result.map(filter => ipFilterToLegacyRule(filter));
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch IP rules';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createIpRuleForKey = useCallback(
    async (virtualKeyId: number, rule: IpRule): Promise<IpRule> => {
      setIsLoading(true);
      setError(null);

      try {
        const createDto = { ...legacyRuleToIpFilter(rule), virtualKeyId };
        const result = await withAdminClient(client =>
          client.ipFilters.create(createDto)
        );
        notify.success('IP rule created successfully');
        return ipFilterToLegacyRule(result);
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to create IP rule';
        setError(message);
        notify.error(err);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    []
  );

  return {
    getIpRules,
    getIpRulesForKey,
    createIpRule,
    createIpRuleForKey,
    updateIpRule,
    deleteIpRule,
    getIpStats,
    isLoading,
    error,
  };
}
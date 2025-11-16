'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import type { 
  IpFilterDto,
  CreateIpFilterDto,
  UpdateIpFilterDto,
  SecurityEvent,
  ThreatDetection,
  SecurityEventFilters,
} from '@knn_labs/conduit-admin-client';

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
    lastMatchedAt: filter.lastMatchedAt,
    matchCount: filter.matchCount,
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

  const getSecurityEvents = useCallback(async (params?: {
    page?: number;
    pageSize?: number;
    severity?: string;
    startDate?: string;
    endDate?: string;
  }): Promise<{ events: SecurityEvent[]; total: number }> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const filters: SecurityEventFilters = {
        page: params?.page,
        pageSize: params?.pageSize,
        severity: params?.severity as SecurityEventFilters['severity'],
        startDate: params?.startDate,
        endDate: params?.endDate,
      };

      const result = await withAdminClient(client =>
        client.security.getEvents(filters)
      );

      return { events: result.items, total: result.totalCount };
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch security events';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getThreats = useCallback(async (params?: {
    status?: 'active' | 'mitigated' | 'resolved';
    severity?: string;
  }): Promise<ThreatDetection[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client =>
        client.security.getThreats()
      );

      // Filter threats based on parameters (client-side filtering since SDK doesn't support it yet)
      let filteredThreats = result;
      
      if (params?.status) {
        // Map 'mitigated' to 'acknowledged' since that's what the API supports
        const mappedStatus = params.status === 'mitigated' ? 'acknowledged' : params.status;
        filteredThreats = result.filter(threat => 
          threat.status === mappedStatus || 
          (params.status === 'mitigated' && threat.status === 'acknowledged')
        );
      }
      
      if (params?.severity) {
        filteredThreats = filteredThreats.filter(threat => 
          threat.severity === params.severity
        );
      }

      return filteredThreats;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch threats';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

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

      notifications.show({
        title: 'Success',
        message: 'IP rule created successfully',
        color: 'green',
      });

      // Convert back to legacy format
      return ipFilterToLegacyRule(result);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create IP rule';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
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

      notifications.show({
        title: 'Success',
        message: 'IP rule updated successfully',
        color: 'green',
      });

      // Return the updated rule (we need to fetch it to get the complete data)
      const updatedFilter = await withAdminClient(client =>
        client.ipFilters.getById(numericId)
      );

      return ipFilterToLegacyRule(updatedFilter);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update IP rule';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
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

      notifications.show({
        title: 'Success',
        message: 'IP rule deleted successfully',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete IP rule';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
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
        blockedRequests24h: filters.reduce((sum, f) => sum + (f.matchCount ?? 0), 0),
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

  return {
    getSecurityEvents,
    getThreats,
    getIpRules,
    createIpRule,
    updateIpRule,
    deleteIpRule,
    getIpStats,
    isLoading,
    error,
  };
}
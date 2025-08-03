'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import type { ErrorResponse } from '@knn_labs/conduit-common';

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

export interface IpStats {
  totalRules: number;
  allowRules: number;
  blockRules: number;
  activeRules: number;
  blockedRequests24h: number;
  lastRuleUpdate: string | null;
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
  }): Promise<{ events: Record<string, unknown>[]; total: number }> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const queryParams = new URLSearchParams();
      if (params?.page) queryParams.append('page', params.page.toString());
      if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
      if (params?.severity) queryParams.append('severity', params.severity);
      if (params?.startDate) queryParams.append('startDate', params.startDate);
      if (params?.endDate) queryParams.append('endDate', params.endDate);

      const response = await fetch(`/api/admin/security/events?${queryParams}`, {
        method: 'GET',
      });

      const result = await response.json() as { events: Record<string, unknown>[]; total: number } | ErrorResponse;

      if (!response.ok) {
        throw new Error((result as ErrorResponse).error ?? 'Failed to fetch security events');
      }

      return result as { events: Record<string, unknown>[]; total: number };
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
  }): Promise<Record<string, unknown>[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const queryParams = new URLSearchParams();
      if (params?.status) queryParams.append('status', params.status);
      if (params?.severity) queryParams.append('severity', params.severity);

      const response = await fetch(`/api/admin/security/threats?${queryParams}`, {
        method: 'GET',
      });

      const result = await response.json() as Record<string, unknown>[] | ErrorResponse;

      if (!response.ok) {
        throw new Error((result as ErrorResponse).error ?? 'Failed to fetch threats');
      }

      return result as Record<string, unknown>[];
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
      const response = await fetch('/api/admin/security/ip-rules', {
        method: 'GET',
      });

      const result = await response.json() as IpRule[] | ErrorResponse;

      if (!response.ok) {
        throw new Error((result as ErrorResponse).error ?? 'Failed to fetch IP rules');
      }

      return result as IpRule[];
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
      const response = await fetch('/api/admin/security/ip-rules', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(rule),
      });

      const result = await response.json() as IpRule | ErrorResponse;

      if (!response.ok) {
        throw new Error((result as ErrorResponse).error ?? 'Failed to create IP rule');
      }

      notifications.show({
        title: 'Success',
        message: 'IP rule created successfully',
        color: 'green',
      });

      return result as IpRule;
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
      const response = await fetch(`/api/admin/security/ip-rules/${id}`, {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(rule),
      });

      const result = await response.json() as IpRule | ErrorResponse;

      if (!response.ok) {
        throw new Error((result as ErrorResponse).error ?? 'Failed to update IP rule');
      }

      notifications.show({
        title: 'Success',
        message: 'IP rule updated successfully',
        color: 'green',
      });

      return result as IpRule;
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
      const response = await fetch(`/api/admin/security/ip-rules/${id}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        const result = await response.json() as ErrorResponse;
        throw new Error(result.error ?? 'Failed to delete IP rule');
      }

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
      const response = await fetch('/api/admin/security/ip-rules/stats', {
        method: 'GET',
      });

      const result = await response.json() as IpStats | ErrorResponse;

      if (!response.ok) {
        throw new Error((result as ErrorResponse).error ?? 'Failed to fetch IP stats');
      }

      return result as IpStats;
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
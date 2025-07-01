import { useEffect, useCallback, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { 
  getSDKSignalRManager, 
  SpendUpdate, 
  SpendLimitAlert 
} from '@/lib/signalr/SDKSignalRManager';
import { logger } from '@/lib/utils/logging';
import { notifications } from '@mantine/notifications';

export interface SpendSummary {
  virtualKeyId: string;
  totalSpend: number;
  limit?: number;
  percentage?: number;
  lastUpdate: Date;
  recentTransactions: SpendTransaction[];
}

export interface SpendTransaction {
  amount: number;
  model: string;
  timestamp: Date;
}

export interface UseSpendTrackingOptions {
  enabled?: boolean;
  virtualKeyIds?: string[];
  onSpendUpdate?: (update: SpendUpdate) => void;
  onSpendLimitAlert?: (alert: SpendLimitAlert) => void;
  showNotifications?: boolean;
  alertThresholds?: {
    warning: number; // percentage (e.g., 75)
    critical: number; // percentage (e.g., 90)
  };
}

export function useSpendTracking(options: UseSpendTrackingOptions = {}) {
  const { 
    enabled = true,
    virtualKeyIds = [],
    onSpendUpdate,
    onSpendLimitAlert,
    showNotifications = true,
    alertThresholds = { warning: 75, critical: 90 },
  } = options;
  
  const queryClient = useQueryClient();
  const [spendSummaries, setSpendSummaries] = useState<Map<string, SpendSummary>>(new Map());
  const [recentAlerts, setRecentAlerts] = useState<SpendLimitAlert[]>([]);

  // Handle spend updates
  const handleSpendUpdate = useCallback((update: SpendUpdate) => {
    logger.debug('Spend update received:', update);

    // Filter by virtualKeyIds if specified
    if (virtualKeyIds.length > 0 && !virtualKeyIds.includes(update.virtualKeyId)) {
      return;
    }

    // Call custom handler
    onSpendUpdate?.(update);

    // Update spend summaries
    setSpendSummaries(prev => {
      const updated = new Map(prev);
      const existing = updated.get(update.virtualKeyId) || {
        virtualKeyId: update.virtualKeyId,
        totalSpend: 0,
        lastUpdate: new Date(),
        recentTransactions: [],
      };

      // Update total spend and add transaction
      existing.totalSpend = update.totalSpend;
      existing.lastUpdate = new Date();
      existing.recentTransactions.unshift({
        amount: update.amount,
        model: update.model,
        timestamp: new Date(update.timestamp),
      });

      // Keep only last 10 transactions
      if (existing.recentTransactions.length > 10) {
        existing.recentTransactions = existing.recentTransactions.slice(0, 10);
      }

      updated.set(update.virtualKeyId, existing);
      return updated;
    });

    // Invalidate virtual key queries to refresh spend data
    queryClient.invalidateQueries({ 
      queryKey: ['admin', 'virtual-keys', update.virtualKeyId] 
    });
    queryClient.invalidateQueries({ 
      queryKey: ['virtual-key-usage', update.virtualKeyId] 
    });
  }, [virtualKeyIds, onSpendUpdate, queryClient]);

  // Handle spend limit alerts
  const handleSpendLimitAlert = useCallback((alert: SpendLimitAlert) => {
    logger.warn('Spend limit alert received:', alert);

    // Filter by virtualKeyIds if specified
    if (virtualKeyIds.length > 0 && !virtualKeyIds.includes(alert.virtualKeyId)) {
      return;
    }

    // Call custom handler
    onSpendLimitAlert?.(alert);

    // Add to recent alerts
    setRecentAlerts(prev => [alert, ...prev.slice(0, 9)]); // Keep last 10 alerts

    // Update spend summary with limit info
    setSpendSummaries(prev => {
      const updated = new Map(prev);
      const existing = updated.get(alert.virtualKeyId);
      if (existing) {
        existing.limit = alert.limit;
        existing.percentage = alert.percentage;
        updated.set(alert.virtualKeyId, existing);
      }
      return updated;
    });

    // Show notification if enabled
    if (showNotifications) {
      const title = alert.alertLevel === 'critical' 
        ? 'Critical Spend Alert!' 
        : 'Spend Warning';
      
      const message = `Virtual key ${alert.virtualKeyId.substring(0, 8)}... has reached ${alert.percentage}% of its ${alert.limit} limit`;
      
      notifications.show({
        title,
        message,
        color: alert.alertLevel === 'critical' ? 'red' : 'yellow',
        autoClose: alert.alertLevel === 'critical' ? false : 10000,
      });
    }

    // Invalidate queries to refresh data
    queryClient.invalidateQueries({ 
      queryKey: ['admin', 'virtual-keys', alert.virtualKeyId] 
    });
  }, [virtualKeyIds, onSpendLimitAlert, showNotifications, queryClient]);

  useEffect(() => {
    if (!enabled) return;

    try {
      // Get SignalR manager
      const signalRManager = getSDKSignalRManager();
      
      // Register event handlers
      signalRManager.on('onSpendUpdate', handleSpendUpdate);
      signalRManager.on('onSpendLimitAlert', handleSpendLimitAlert);

      logger.info('Spend tracking hub listeners registered');

      // Cleanup
      return () => {
        signalRManager.off('onSpendUpdate');
        signalRManager.off('onSpendLimitAlert');
        logger.info('Spend tracking hub listeners unregistered');
      };
    } catch (error) {
      logger.error('Failed to setup spend tracking hub:', error);
    }
  }, [enabled, handleSpendUpdate, handleSpendLimitAlert]);

  // Get spend summary for a specific virtual key
  const getSpendSummary = useCallback((virtualKeyId: string): SpendSummary | undefined => {
    return spendSummaries.get(virtualKeyId);
  }, [spendSummaries]);

  // Get all spend summaries
  const getAllSpendSummaries = useCallback((): SpendSummary[] => {
    return Array.from(spendSummaries.values());
  }, [spendSummaries]);

  // Get keys approaching limits
  const getKeysApproachingLimits = useCallback((threshold: number = 75): SpendSummary[] => {
    return Array.from(spendSummaries.values()).filter(
      summary => summary.percentage && summary.percentage >= threshold
    );
  }, [spendSummaries]);

  // Clear alerts
  const clearAlerts = useCallback(() => {
    setRecentAlerts([]);
  }, []);

  // Check if any key is over threshold
  const hasWarnings = useCallback((): boolean => {
    return Array.from(spendSummaries.values()).some(
      summary => summary.percentage && summary.percentage >= alertThresholds.warning
    );
  }, [spendSummaries, alertThresholds]);

  const hasCriticalAlerts = useCallback((): boolean => {
    return Array.from(spendSummaries.values()).some(
      summary => summary.percentage && summary.percentage >= alertThresholds.critical
    );
  }, [spendSummaries, alertThresholds]);

  return {
    spendSummaries: getAllSpendSummaries(),
    recentAlerts,
    getSpendSummary,
    getKeysApproachingLimits,
    clearAlerts,
    hasWarnings: hasWarnings(),
    hasCriticalAlerts: hasCriticalAlerts(),
    isConnected: enabled,
  };
}

// Hook for monitoring spend for a specific virtual key
export function useVirtualKeySpend(virtualKeyId: string | null) {
  const [spendData, setSpendData] = useState<{
    totalSpend: number;
    limit?: number;
    percentage?: number;
    trend: 'up' | 'down' | 'stable';
    recentActivity: SpendTransaction[];
  } | null>(null);

  const handleSpendUpdate = useCallback((update: SpendUpdate) => {
    if (update.virtualKeyId !== virtualKeyId) return;

    setSpendData(prev => {
      const newTotal = update.totalSpend;
      const oldTotal = prev?.totalSpend || 0;
      
      return {
        totalSpend: newTotal,
        limit: prev?.limit,
        percentage: prev?.percentage,
        trend: newTotal > oldTotal ? 'up' : newTotal < oldTotal ? 'down' : 'stable',
        recentActivity: [
          {
            amount: update.amount,
            model: update.model,
            timestamp: new Date(update.timestamp),
          },
          ...(prev?.recentActivity || []).slice(0, 4), // Keep last 5
        ],
      };
    });
  }, [virtualKeyId]);

  const handleLimitAlert = useCallback((alert: SpendLimitAlert) => {
    if (alert.virtualKeyId !== virtualKeyId) return;

    setSpendData(prev => prev ? {
      ...prev,
      limit: alert.limit,
      percentage: alert.percentage,
    } : null);
  }, [virtualKeyId]);

  const { spendSummaries } = useSpendTracking({
    enabled: !!virtualKeyId,
    virtualKeyIds: virtualKeyId ? [virtualKeyId] : [],
    onSpendUpdate: handleSpendUpdate,
    onSpendLimitAlert: handleLimitAlert,
    showNotifications: false, // Don't show notifications for individual key monitoring
  });

  // Initialize from summaries if available
  useEffect(() => {
    if (virtualKeyId && !spendData) {
      const summary = spendSummaries.find(s => s.virtualKeyId === virtualKeyId);
      if (summary) {
        setSpendData({
          totalSpend: summary.totalSpend,
          limit: summary.limit,
          percentage: summary.percentage,
          trend: 'stable',
          recentActivity: summary.recentTransactions,
        });
      }
    }
  }, [virtualKeyId, spendData, spendSummaries]);

  return {
    totalSpend: spendData?.totalSpend || 0,
    limit: spendData?.limit,
    percentage: spendData?.percentage || 0,
    trend: spendData?.trend || 'stable',
    recentActivity: spendData?.recentActivity || [],
    isApproachingLimit: (spendData?.percentage || 0) >= 75,
    isOverLimit: (spendData?.percentage || 0) >= 100,
  };
}
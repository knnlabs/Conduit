/**
 * Unified system status management hook
 * Consolidates status logic from multiple components and provides consistent status indicators
 */

import { useState, useEffect, useCallback } from 'react';
import { badgeHelpers } from '@/lib/utils/badge-helpers';
import { useConnectionStore } from '@/stores/useConnectionStore';

export type SystemStatusType = 
  | 'healthy' 
  | 'degraded' 
  | 'unhealthy' 
  | 'maintenance' 
  | 'unknown' 
  | 'connecting' 
  | 'disconnected'
  | 'enabled'
  | 'disabled'
  | 'pending'
  | 'processing'
  | 'completed'
  | 'failed'
  | 'warning'
  | 'error';

export type ConnectionStatusType = 
  | 'connected' 
  | 'connecting' 
  | 'disconnected' 
  | 'reconnecting' 
  | 'error';

export interface StatusConfig {
  type: SystemStatusType;
  color: string;
  label: string;
  icon: string;
  description?: string;
  priority: 'low' | 'medium' | 'high' | 'critical';
}

export interface SystemStatusSnapshot {
  core: StatusConfig;
  admin: StatusConfig;
  signalR: StatusConfig;
  overall: StatusConfig;
  isHealthy: boolean;
  hasWarnings: boolean;
  hasErrors: boolean;
  lastUpdate: Date;
}

export interface UseSystemStatusOptions {
  autoRefresh?: boolean;
  refreshInterval?: number;
  includeMetrics?: boolean;
}

export function useSystemStatus(options: UseSystemStatusOptions = {}) {
  const {
    autoRefresh = true,
    refreshInterval = 30000, // 30 seconds
    includeMetrics = false,
  } = options;

  const { status: connectionStatus } = useConnectionStore();
  const [systemStatus, setSystemStatus] = useState<SystemStatusSnapshot | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  // Status configuration mapping
  const getStatusConfig = useCallback((status: SystemStatusType, context?: string): StatusConfig => {
    const baseConfig = badgeHelpers.getStatusConfig(status, context as any);
    
    const statusConfigs: Record<SystemStatusType, Partial<StatusConfig>> = {
      healthy: {
        icon: 'check-circle',
        priority: 'low',
        description: 'All systems operational',
      },
      degraded: {
        icon: 'alert-triangle',
        priority: 'medium',
        description: 'Some services experiencing issues',
      },
      unhealthy: {
        icon: 'x-circle',
        priority: 'high',
        description: 'Critical systems offline',
      },
      maintenance: {
        icon: 'tool',
        priority: 'medium',
        description: 'Scheduled maintenance in progress',
      },
      unknown: {
        icon: 'help-circle',
        priority: 'low',
        description: 'Status cannot be determined',
      },
      connecting: {
        icon: 'loader',
        priority: 'low',
        description: 'Establishing connection',
      },
      disconnected: {
        icon: 'wifi-off',
        priority: 'high',
        description: 'Connection lost',
      },
      enabled: {
        icon: 'check',
        priority: 'low',
        description: 'Service is enabled',
      },
      disabled: {
        icon: 'x',
        priority: 'low',
        description: 'Service is disabled',
      },
      pending: {
        icon: 'clock',
        priority: 'low',
        description: 'Operation pending',
      },
      processing: {
        icon: 'activity',
        priority: 'low',
        description: 'Operation in progress',
      },
      completed: {
        icon: 'check-circle',
        priority: 'low',
        description: 'Operation completed successfully',
      },
      failed: {
        icon: 'x-circle',
        priority: 'high',
        description: 'Operation failed',
      },
      warning: {
        icon: 'alert-triangle',
        priority: 'medium',
        description: 'Warning condition detected',
      },
      error: {
        icon: 'alert-circle',
        priority: 'high',
        description: 'Error condition detected',
      },
    };

    const specificConfig = statusConfigs[status] || {};
    
    return {
      type: status,
      color: baseConfig.color || 'gray',
      label: baseConfig.label || status,
      icon: specificConfig.icon || 'help-circle',
      description: specificConfig.description,
      priority: specificConfig.priority || 'low',
      ...specificConfig,
    };
  }, []);

  // Determine overall system status from individual components
  const calculateOverallStatus = useCallback((
    coreStatus: StatusConfig,
    adminStatus: StatusConfig,
    signalRStatus: StatusConfig
  ): StatusConfig => {
    const priorities = [coreStatus.priority, adminStatus.priority, signalRStatus.priority];
    const types = [coreStatus.type, adminStatus.type, signalRStatus.type];

    // If any critical issues, overall is unhealthy
    if (priorities.includes('critical') || types.includes('unhealthy')) {
      return getStatusConfig('unhealthy');
    }

    // If any high priority issues, overall is degraded
    if (priorities.includes('high') || types.includes('degraded') || types.includes('error')) {
      return getStatusConfig('degraded');
    }

    // If any warnings, overall is degraded
    if (priorities.includes('medium') || types.includes('warning')) {
      return getStatusConfig('degraded');
    }

    // If any connecting states, overall is connecting
    if (types.includes('connecting')) {
      return getStatusConfig('connecting');
    }

    // If all are healthy, overall is healthy
    if (types.every(type => ['healthy', 'enabled', 'completed'].includes(type))) {
      return getStatusConfig('healthy');
    }

    // Default to unknown
    return getStatusConfig('unknown');
  }, [getStatusConfig]);

  // Map connection store status to our status types
  const mapConnectionStatus = useCallback((status: any): SystemStatusType => {
    if (!status) return 'unknown';
    
    switch (status) {
      case 'connected':
      case 'online':
      case 'operational':
        return 'healthy';
      case 'connecting':
      case 'reconnecting':
        return 'connecting';
      case 'disconnected':
      case 'offline':
      case 'error':
        return 'disconnected';
      case 'degraded':
      case 'warning':
        return 'degraded';
      case 'maintenance':
        return 'maintenance';
      default:
        return 'unknown';
    }
  }, []);

  // Refresh system status
  const refreshStatus = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);

      // Map connection store statuses to our status configs
      const coreStatus = getStatusConfig(mapConnectionStatus(connectionStatus.coreApi));
      const adminStatus = getStatusConfig(mapConnectionStatus(connectionStatus.adminApi));
      const signalRStatus = getStatusConfig(mapConnectionStatus(connectionStatus.signalR));
      
      const overall = calculateOverallStatus(coreStatus, adminStatus, signalRStatus);

      const snapshot: SystemStatusSnapshot = {
        core: coreStatus,
        admin: adminStatus,
        signalR: signalRStatus,
        overall,
        isHealthy: overall.type === 'healthy',
        hasWarnings: overall.priority === 'medium',
        hasErrors: ['high', 'critical'].includes(overall.priority),
        lastUpdate: new Date(),
      };

      setSystemStatus(snapshot);
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Failed to fetch system status'));
    } finally {
      setIsLoading(false);
    }
  }, [connectionStatus, getStatusConfig, mapConnectionStatus, calculateOverallStatus]);

  // Auto-refresh effect
  useEffect(() => {
    refreshStatus();

    if (autoRefresh && refreshInterval > 0) {
      const interval = setInterval(refreshStatus, refreshInterval);
      return () => clearInterval(interval);
    }
  }, [refreshStatus, autoRefresh, refreshInterval]);

  // Helper functions for common status operations
  const isSystemHealthy = useCallback(() => {
    return systemStatus?.isHealthy ?? false;
  }, [systemStatus]);

  const getSystemAlerts = useCallback(() => {
    if (!systemStatus) return [];

    const alerts = [];
    
    if (systemStatus.hasErrors) {
      alerts.push({
        type: 'error' as const,
        message: 'Critical system issues detected',
        details: systemStatus.overall.description,
      });
    }
    
    if (systemStatus.hasWarnings) {
      alerts.push({
        type: 'warning' as const,
        message: 'System performance degraded',
        details: systemStatus.overall.description,
      });
    }

    return alerts;
  }, [systemStatus]);

  const getComponentStatus = useCallback((component: 'core' | 'admin' | 'signalR') => {
    return systemStatus?.[component] ?? getStatusConfig('unknown');
  }, [systemStatus, getStatusConfig]);

  // Utility functions for component status checking
  const getHealthColor = useCallback((status: SystemStatusType) => {
    return badgeHelpers.getHealthColor(status);
  }, []);

  const getStatusColor = useCallback((isEnabled: boolean) => {
    return badgeHelpers.getStatusColor(isEnabled);
  }, []);

  const formatStatus = useCallback((status: SystemStatusType | boolean, options?: { activeText?: string; inactiveText?: string }) => {
    if (typeof status === 'boolean') {
      return badgeHelpers.formatStatus(status, options);
    }
    return getStatusConfig(status).label;
  }, [getStatusConfig]);

  return {
    // Core status data
    systemStatus,
    isLoading,
    error,
    
    // Status checking utilities
    isSystemHealthy,
    getSystemAlerts,
    getComponentStatus,
    
    // Utility functions
    getStatusConfig,
    getHealthColor,
    getStatusColor,
    formatStatus,
    
    // Actions
    refreshStatus,
  };
}

// Additional utility hook for simple status indicators
export function useStatusIndicator(status: SystemStatusType | boolean, context?: string) {
  const { getStatusConfig, getStatusColor, formatStatus } = useSystemStatus({ autoRefresh: false });
  
  if (typeof status === 'boolean') {
    return {
      color: getStatusColor(status),
      label: formatStatus(status),
      type: status ? 'enabled' : 'disabled',
    };
  }
  
  const config = getStatusConfig(status, context);
  return {
    color: config.color,
    label: config.label,
    type: config.type,
    icon: config.icon,
    description: config.description,
    priority: config.priority,
  };
}

// Hook for connection-specific status management
export function useConnectionStatus() {
  const { status: connectionStatus } = useConnectionStore();
  const { getStatusConfig } = useSystemStatus({ autoRefresh: false });

  const getConnectionStatusConfig = useCallback((connectionType: 'coreApi' | 'adminApi' | 'signalR') => {
    const status = connectionStatus[connectionType];
    
    switch (status) {
      case 'connected':
        return getStatusConfig('healthy');
      case 'connecting':
        return getStatusConfig('connecting');
      case 'disconnected':
        return getStatusConfig('disconnected');
      case 'error':
        return getStatusConfig('error');
      default:
        return getStatusConfig('unknown');
    }
  }, [connectionStatus, getStatusConfig]);

  return {
    connectionStatus,
    getCoreStatus: () => getConnectionStatusConfig('coreApi'),
    getAdminStatus: () => getConnectionStatusConfig('adminApi'),
    getSignalRStatus: () => getConnectionStatusConfig('signalR'),
  };
}
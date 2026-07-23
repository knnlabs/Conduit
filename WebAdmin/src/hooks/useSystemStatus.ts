/**
 * Status configuration utilities for the shared status indicator components.
 */

import { badgeHelpers } from '@/lib/utils/badge-helpers';

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

export interface StatusConfig {
  type: SystemStatusType;
  color: string;
  label: string;
  icon: string;
  description?: string;
  priority: 'low' | 'medium' | 'high' | 'critical';
}

type StatusIndicatorConfig = Omit<StatusConfig, 'icon' | 'priority'> &
  Partial<Pick<StatusConfig, 'icon' | 'priority'>>;

const STATUS_CONFIGS: Record<
  SystemStatusType,
  Pick<StatusConfig, 'icon' | 'priority' | 'description'>
> = {
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

function getStatusConfig(status: SystemStatusType): StatusConfig {
  const baseConfig = badgeHelpers.getStatusConfig(status);
  const specificConfig = STATUS_CONFIGS[status];

  return {
    type: status,
    color: baseConfig.color ?? 'gray',
    label: baseConfig.label ?? status,
    ...specificConfig,
  };
}

// Keep the hook-shaped API used by StatusIndicator while avoiding any hidden
// connection state or refresh lifecycle.
export function useStatusIndicator(status: SystemStatusType | boolean): StatusIndicatorConfig {
  if (typeof status === 'boolean') {
    return {
      color: badgeHelpers.getStatusColor(status),
      label: badgeHelpers.formatStatus(status),
      type: status ? 'enabled' : 'disabled',
    };
  }

  return getStatusConfig(status);
}

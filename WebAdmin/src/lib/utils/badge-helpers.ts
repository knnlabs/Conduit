/**
 * Standardized badge and status utilities for consistent UI presentation
 */

export type StatusType = 'enabled' | 'health' | 'connection' | 'progress' | 'priority';
export type ColorTheme = 'green' | 'red' | 'yellow' | 'blue' | 'gray' | 'orange' | 'purple';

export interface StatusConfig {
  color: ColorTheme;
  label: string;
  icon?: string;
}

/**
 * Badge and status color utilities with comprehensive theming
 */
export const badgeHelpers = {
  /**
   * Get standardized color for boolean status
   */
  getStatusColor: (
    enabled: boolean | null | undefined,
    type: StatusType = 'enabled'
  ): ColorTheme => {
    if (enabled === null || enabled === undefined) return 'gray';
    
    switch (type) {
      case 'enabled':
      case 'connection':
        return enabled ? 'green' : 'red';
      case 'health':
        return enabled ? 'green' : 'red';
      default:
        return enabled ? 'green' : 'red';
    }
  },

  /**
   * Get color for health status with multiple states
   */
  getHealthColor: (health: string | null | undefined): ColorTheme => {
    if (!health) return 'gray';
    
    const normalizedHealth = health.toLowerCase();
    switch (normalizedHealth) {
      case 'healthy':
      case 'online':
      case 'connected':
      case 'active':
        return 'green';
      case 'degraded':
      case 'warning':
      case 'slow':
        return 'yellow';
      case 'unhealthy':
      case 'offline':
      case 'disconnected':
      case 'failed':
      case 'error':
        return 'red';
      case 'pending':
      case 'loading':
      case 'connecting':
        return 'blue';
      case 'maintenance':
      case 'paused':
        return 'orange';
      default:
        return 'gray';
    }
  },

  /**
   * Get color based on percentage thresholds
   */
  getPercentageColor: (
    percentage: number | null | undefined,
    thresholds: { danger?: number; warning?: number; good?: number } = {}
  ): ColorTheme => {
    if (percentage === null || percentage === undefined || isNaN(percentage)) {
      return 'gray';
    }

    const { danger = 90, warning = 70, good = 50 } = thresholds;

    if (percentage >= danger) return 'red';
    if (percentage >= warning) return 'yellow';
    if (percentage >= good) return 'orange';
    return 'green';
  },

  /**
   * Get color for priority levels
   */
  getPriorityColor: (priority: string | number | null | undefined): ColorTheme => {
    if (priority === null || priority === undefined) return 'gray';
    
    const normalizedPriority = typeof priority === 'string' 
      ? priority.toLowerCase() 
      : priority;

    switch (normalizedPriority) {
      case 'critical':
      case 'high':
      case 1:
        return 'red';
      case 'medium':
      case 'moderate':
      case 2:
        return 'yellow';
      case 'low':
      case 3:
        return 'blue';
      case 'none':
      case 'info':
      case 4:
        return 'gray';
      default:
        return 'gray';
    }
  },

  /**
   * Format status text with consistent conventions
   */
  formatStatus: (
    enabled: boolean | null | undefined,
    options: {
      activeText?: string;
      inactiveText?: string;
      unknownText?: string;
      capitalize?: boolean;
    } = {}
  ): string => {
    const {
      activeText = 'Active',
      inactiveText = 'Inactive',
      unknownText = 'Unknown',
      capitalize = true
    } = options;

    let text: string;
    if (enabled === true) text = activeText;
    else if (enabled === false) text = inactiveText;
    else text = unknownText;

    return capitalize ? text : text.toLowerCase();
  },

  /**
   * Get complete status configuration
   */
  getStatusConfig: (
    status: boolean | string | null | undefined,
    type: StatusType = 'enabled'
  ): StatusConfig => {
    if (typeof status === 'boolean') {
      return {
        color: badgeHelpers.getStatusColor(status, type),
        label: badgeHelpers.formatStatus(status)
      };
    }

    if (typeof status === 'string') {
      return {
        color: badgeHelpers.getHealthColor(status),
        label: status.charAt(0).toUpperCase() + status.slice(1).toLowerCase()
      };
    }

    return {
      color: 'gray',
      label: 'Unknown'
    };
  }
};
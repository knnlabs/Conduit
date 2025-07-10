'use client';

import type { TimeRangeFilter, DateRange } from '@/types/analytics-types';

/**
 * Combined UI utility functions for analytics, badges, and formatting
 * Consolidates analytics-helpers.ts and badge-helpers.ts
 */

// ===== BADGE & STATUS UTILITIES =====

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

// ===== ANALYTICS & FORMATTING UTILITIES =====

/**
 * Convert a TimeRangeFilter to a DateRange for SDK usage
 */
export function convertTimeRangeToDateRange(timeRange: TimeRangeFilter): DateRange {
  if (timeRange.range === 'custom' && timeRange.startDate && timeRange.endDate) {
    return {
      startDate: timeRange.startDate,
      endDate: timeRange.endDate,
    };
  }
  
  const now = new Date();
  let startDate: Date;
  
  switch (timeRange.range) {
    case '1h':
      startDate = new Date(now.getTime() - 60 * 60 * 1000);
      break;
    case '24h':
      startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000);
      break;
    case '7d':
      startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      break;
    case '30d':
      startDate = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
      break;
    case '90d':
      startDate = new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
      break;
    default:
      startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000);
  }
  
  return {
    startDate: startDate.toISOString(),
    endDate: now.toISOString(),
  };
}

/**
 * Get the appropriate groupBy value for analytics based on time range
 */
export function getGroupByFromTimeRange(timeRange: TimeRangeFilter): 'hour' | 'day' | 'week' | 'month' {
  switch (timeRange.range) {
    case '1h':
    case '24h':
      return 'hour';
    case '7d':
      return 'day';
    case '30d':
      return 'day';
    case '90d':
      return 'week';
    default:
      return 'day';
  }
}

/**
 * Calculate percentage change between two values
 */
export function calculatePercentageChange(current: number, previous: number): number {
  if (previous === 0) return current > 0 ? 100 : 0;
  return Math.round(((current - previous) / previous) * 1000) / 10;
}

/**
 * Determine trend direction based on current and previous values
 */
export function calculateTrend(current: number, previous: number, threshold = 0.05): 'up' | 'down' | 'stable' {
  const change = Math.abs(current - previous);
  const thresholdValue = Math.max(previous * threshold, 1); // At least 1 unit change
  if (change < thresholdValue) return 'stable';
  return current > previous ? 'up' : 'down';
}

/**
 * Format a number as currency
 */
export function formatCurrency(value: number, decimals = 2): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value);
}

/**
 * Format a large number with abbreviations (K, M, B)
 */
export function formatLargeNumber(value: number, decimals = 1): string {
  if (value < 1000) return value.toString();
  if (value < 1000000) return `${(value / 1000).toFixed(decimals)}K`;
  if (value < 1000000000) return `${(value / 1000000).toFixed(decimals)}M`;
  return `${(value / 1000000000).toFixed(decimals)}B`;
}

/**
 * Calculate percentile from sorted array
 */
export function getPercentile(sortedArray: number[], percentile: number): number {
  if (sortedArray.length === 0) return 0;
  const index = Math.floor((percentile / 100) * sortedArray.length);
  return sortedArray[Math.min(index, sortedArray.length - 1)];
}

/**
 * Group request logs by time period
 */
export function groupByTimePeriod<T extends { timestamp: string }>(
  logs: T[],
  groupBy: 'hour' | 'day' | 'week' | 'month'
): Map<string, T[]> {
  const grouped = new Map<string, T[]>();
  
  logs.forEach(log => {
    const date = new Date(log.timestamp);
    let key: string;
    
    switch (groupBy) {
      case 'hour':
        date.setMinutes(0, 0, 0);
        key = date.toISOString();
        break;
      case 'day':
        date.setHours(0, 0, 0, 0);
        key = date.toISOString().split('T')[0];
        break;
      case 'week':
        const weekStart = new Date(date);
        weekStart.setDate(date.getDate() - date.getDay());
        weekStart.setHours(0, 0, 0, 0);
        key = weekStart.toISOString().split('T')[0];
        break;
      case 'month':
        date.setDate(1);
        date.setHours(0, 0, 0, 0);
        key = date.toISOString().split('T')[0];
        break;
    }
    
    const existing = grouped.get(key) || [];
    existing.push(log);
    grouped.set(key, existing);
  });
  
  return grouped;
}

/**
 * Determine provider from model name
 */
export function getProviderFromModel(modelName: string): string {
  const modelLower = modelName.toLowerCase();
  
  if (modelLower.includes('gpt') || modelLower.includes('dall-e') || modelLower.includes('whisper')) {
    return modelLower.includes('azure') ? 'Azure OpenAI' : 'OpenAI';
  } else if (modelLower.includes('claude')) {
    return 'Anthropic';
  } else if (modelLower.includes('minimax')) {
    return 'MiniMax';
  } else if (modelLower.includes('llama') || modelLower.includes('mistral')) {
    return 'Replicate';
  }
  
  return 'Unknown';
}

/**
 * Get endpoint type from model name
 */
export function getEndpointFromModel(modelName: string): string {
  const modelLower = modelName.toLowerCase();
  
  if (modelLower.includes('chat') || modelLower.includes('gpt') || modelLower.includes('claude')) {
    return '/v1/chat/completions';
  } else if (modelLower.includes('dall-e')) {
    return '/v1/images/generations';
  } else if (modelLower.includes('whisper')) {
    return '/v1/audio/transcriptions';
  } else if (modelLower.includes('tts')) {
    return '/v1/audio/speech';
  } else if (modelLower.includes('embed')) {
    return '/v1/embeddings';
  }
  
  return '/v1/chat/completions'; // Default
}

/**
 * Create export filename with timestamp
 */
export function createExportFilename(prefix: string, type: string, format: string): string {
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  return `${prefix}-${type}-${timestamp}.${format}`;
}

/**
 * Calculate time range in hours
 */
export function getTimeRangeHours(timeRange: TimeRangeFilter): number {
  switch (timeRange.range) {
    case '1h':
      return 1;
    case '24h':
      return 24;
    case '7d':
      return 168;
    case '30d':
      return 720;
    case '90d':
      return 2160;
    default:
      if (timeRange.startDate && timeRange.endDate) {
        const start = new Date(timeRange.startDate);
        const end = new Date(timeRange.endDate);
        return Math.floor((end.getTime() - start.getTime()) / (60 * 60 * 1000));
      }
      return 24; // Default to 24 hours
  }
}
/**
 * Shared analytics utilities and calculations
 */

export interface DateRange extends Record<string, string> {
  startDate: string;
  endDate: string;
}

export type TimeRangeFilter = '1h' | '24h' | '7d' | '30d' | '90d' | 'custom';

export interface CustomTimeRange {
  type: 'custom';
  customStart: Date;
  customEnd: Date;
}

export type TimeRangeOption = TimeRangeFilter | CustomTimeRange;

export const analyticsHelpers = {
  /**
   * Convert time range filter to concrete date range
   */
  convertTimeRangeToDateRange(timeRange: TimeRangeOption): DateRange {
    const now = new Date();
    
    if (typeof timeRange === 'object' && timeRange.type === 'custom') {
      return {
        startDate: timeRange.customStart.toISOString(),
        endDate: timeRange.customEnd.toISOString()
      };
    }

    switch (timeRange) {
      case '1h':
        return {
          startDate: new Date(now.getTime() - 60 * 60 * 1000).toISOString(),
          endDate: now.toISOString()
        };
      case '24h':
        return {
          startDate: new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString(),
          endDate: now.toISOString()
        };
      case '7d':
        return {
          startDate: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString(),
          endDate: now.toISOString()
        };
      case '30d':
        return {
          startDate: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString(),
          endDate: now.toISOString()
        };
      case '90d':
        return {
          startDate: new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000).toISOString(),
          endDate: now.toISOString()
        };
      default:
        return { startDate: now.toISOString(), endDate: now.toISOString() };
    }
  },

  /**
   * Get appropriate grouping for time range
   */
  getGroupByFromTimeRange(timeRange: TimeRangeOption): 'hour' | 'day' | 'week' | 'month' {
    if (typeof timeRange === 'object' && timeRange.type === 'custom') {
      const durationMs = timeRange.customEnd.getTime() - timeRange.customStart.getTime();
      const days = durationMs / (1000 * 60 * 60 * 24);
      
      if (days <= 1) return 'hour';
      if (days <= 7) return 'day';
      if (days <= 30) return 'day';
      if (days <= 90) return 'week';
      return 'month';
    }

    switch (timeRange) {
      case '1h':
      case '24h':
        return 'hour';
      case '7d':
      case '30d':
        return 'day';
      case '90d':
        return 'week';
      default:
        return 'day';
    }
  },

  /**
   * Aggregate cost data with consistent calculations
   */
  aggregateCosts<T extends { cost: number; requests: number }>(data: T[]): {
    totalCost: number;
    requestCount: number;
    averageCostPerRequest: number;
  } {
    const result = data.reduce((acc, item) => ({
      totalCost: acc.totalCost + item.cost,
      requestCount: acc.requestCount + item.requests
    }), { totalCost: 0, requestCount: 0 });

    return {
      ...result,
      averageCostPerRequest: result.requestCount > 0 
        ? result.totalCost / result.requestCount 
        : 0
    };
  },

  /**
   * Calculate cost trends and comparisons
   */
  calculateTrends(current: number, previous: number): {
    change: number;
    percentageChange: number;
    trend: 'up' | 'down' | 'stable';
  } {
    const change = current - previous;
    const percentageChange = previous === 0 
      ? (current > 0 ? 100 : 0) 
      : (change / previous) * 100;
    
    return {
      change,
      percentageChange,
      trend: Math.abs(percentageChange) < 1 ? 'stable' : 
             percentageChange > 0 ? 'up' : 'down'
    };
  },

  /**
   * Calculate growth rate between two values
   */
  calculateGrowthRate(current: number, previous: number): number {
    if (previous === 0) return current > 0 ? 1 : 0;
    return (current - previous) / previous;
  },

  /**
   * Format percentage change with sign
   */
  formatPercentageChange(change: number): string {
    const sign = change > 0 ? '+' : '';
    return `${sign}${change.toFixed(1)}%`;
  },

  /**
   * Get previous period date range for comparisons
   */
  getPreviousPeriod(dateRange: DateRange): DateRange {
    const startDate = new Date(dateRange.startDate);
    const endDate = new Date(dateRange.endDate);
    const duration = endDate.getTime() - startDate.getTime();
    return {
      startDate: new Date(startDate.getTime() - duration).toISOString(),
      endDate: new Date(endDate.getTime() - duration).toISOString()
    };
  },

  /**
   * Check if date is within range
   */
  isDateInRange(date: Date, range: DateRange): boolean {
    const startDate = new Date(range.startDate);
    const endDate = new Date(range.endDate);
    return date >= startDate && date <= endDate;
  },

  /**
   * Get start of period (day, week, month)
   */
  getStartOfPeriod(date: Date, period: 'day' | 'week' | 'month'): Date {
    const result = new Date(date);
    
    switch (period) {
      case 'day':
        result.setHours(0, 0, 0, 0);
        break;
      case 'week':
        const day = result.getDay();
        const diff = result.getDate() - day;
        result.setDate(diff);
        result.setHours(0, 0, 0, 0);
        break;
      case 'month':
        result.setDate(1);
        result.setHours(0, 0, 0, 0);
        break;
    }
    
    return result;
  },

  /**
   * Get end of period (day, week, month)
   */
  getEndOfPeriod(date: Date, period: 'day' | 'week' | 'month'): Date {
    const result = new Date(date);
    
    switch (period) {
      case 'day':
        result.setHours(23, 59, 59, 999);
        break;
      case 'week':
        const day = result.getDay();
        const diff = result.getDate() - day + 6;
        result.setDate(diff);
        result.setHours(23, 59, 59, 999);
        break;
      case 'month':
        result.setMonth(result.getMonth() + 1);
        result.setDate(0);
        result.setHours(23, 59, 59, 999);
        break;
    }
    
    return result;
  },

  /**
   * Calculate simple moving average
   */
  calculateMovingAverage(data: number[], window: number): number[] {
    if (data.length < window) return [];
    
    const result: number[] = [];
    
    for (let i = window - 1; i < data.length; i++) {
      let sum = 0;
      for (let j = 0; j < window; j++) {
        sum += data[i - j];
      }
      result.push(sum / window);
    }
    
    return result;
  },

  /**
   * Normalize value to percentage (0-100)
   */
  normalizeToPercentage(value: number, min: number, max: number): number {
    if (max === min) return 0;
    return ((value - min) / (max - min)) * 100;
  },

  /**
   * Get time range display label
   */
  getTimeRangeLabel(timeRange: TimeRangeOption): string {
    if (typeof timeRange === 'object' && timeRange.type === 'custom') {
      return 'Custom Range';
    }

    const labels: Record<TimeRangeFilter, string> = {
      '1h': 'Last Hour',
      '24h': 'Last 24 Hours',
      '7d': 'Last 7 Days',
      '30d': 'Last 30 Days',
      '90d': 'Last 90 Days',
      'custom': 'Custom Range'
    };

    return labels[timeRange as TimeRangeFilter] || 'Custom Range';
  },

  /**
   * Calculate compound annual growth rate (CAGR)
   */
  calculateCAGR(beginValue: number, endValue: number, numPeriods: number): number {
    if (beginValue <= 0 || numPeriods === 0) return 0;
    return Math.pow(endValue / beginValue, 1 / numPeriods) - 1;
  },

  /**
   * Calculate standard deviation
   */
  calculateStandardDeviation(values: number[]): number {
    if (values.length === 0) return 0;
    
    const mean = values.reduce((sum, val) => sum + val, 0) / values.length;
    const squaredDiffs = values.map(val => Math.pow(val - mean, 2));
    const avgSquaredDiff = squaredDiffs.reduce((sum, val) => sum + val, 0) / values.length;
    
    return Math.sqrt(avgSquaredDiff);
  },

  /**
   * Find outliers in dataset using IQR method
   */
  findOutliers(data: number[]): { lower: number[]; upper: number[] } {
    if (data.length < 4) return { lower: [], upper: [] };
    
    const sorted = [...data].sort((a, b) => a - b);
    const q1Index = Math.floor(sorted.length * 0.25);
    const q3Index = Math.floor(sorted.length * 0.75);
    
    const q1 = sorted[q1Index];
    const q3 = sorted[q3Index];
    const iqr = q3 - q1;
    
    const lowerBound = q1 - 1.5 * iqr;
    const upperBound = q3 + 1.5 * iqr;
    
    return {
      lower: data.filter(val => val < lowerBound),
      upper: data.filter(val => val > upperBound)
    };
  }
};
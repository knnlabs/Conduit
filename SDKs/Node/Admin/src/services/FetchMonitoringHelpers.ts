import type {
  MetricsResponse,
  AlertDto,
  AlertSeverity,
  AlertAction,
  SystemResourceMetrics,
} from '../models/monitoring';

/**
 * Helper utilities for monitoring service operations
 */
export class FetchMonitoringHelpers {
  /**
   * Calculate metric statistics
   */
  calculateMetricStats(series: MetricsResponse['series'][0]): {
    min: number;
    max: number;
    avg: number;
    sum: number;
    count: number;
    stdDev: number;
  } {
    const values = series.dataPoints.map(p => p.value);
    
    if (values.length === 0) {
      return { min: 0, max: 0, avg: 0, sum: 0, count: 0, stdDev: 0 };
    }

    const min = Math.min(...values);
    const max = Math.max(...values);
    const sum = values.reduce((a, b) => a + b, 0);
    const count = values.length;
    const avg = sum / count;
    
    const variance = values.reduce((acc, val) => acc + Math.pow(val - avg, 2), 0) / count;
    const stdDev = Math.sqrt(variance);

    return { min, max, avg, sum, count, stdDev };
  }

  /**
   * Format metric value with unit
   */
  formatMetricValue(value: number, unit: string): string {
    switch (unit) {
      case 'bytes':
        return this.formatBytes(value);
      case 'milliseconds':
        return `${value.toFixed(2)}ms`;
      case 'seconds':
        return `${value.toFixed(2)}s`;
      case 'percentage':
        return `${value.toFixed(2)}%`;
      case 'count':
        return value.toLocaleString();
      default:
        return `${value.toFixed(2)} ${unit}`;
    }
  }

  /**
   * Format bytes to human readable format
   */
  private formatBytes(bytes: number): string {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;

    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }

    return `${size.toFixed(2)} ${units[unitIndex]}`;
  }

  /**
   * Generate alert summary message
   */
  generateAlertSummary(alerts: AlertDto[]): string {
    const byStatus = alerts.reduce((acc, alert) => {
      acc[alert.status] = (acc[alert.status] ?? 0) + 1;
      return acc;
    }, {} as Record<string, number>);

    const bySeverity = alerts.reduce((acc, alert) => {
      acc[alert.severity] = (acc[alert.severity] ?? 0) + 1;
      return acc;
    }, {} as Record<string, number>);

    const parts = [];
    
    if (byStatus.active > 0) {
      parts.push(`${byStatus.active} active`);
    }
    if (byStatus.acknowledged > 0) {
      parts.push(`${byStatus.acknowledged} acknowledged`);
    }
    
    const severityParts = [];
    if (bySeverity.critical > 0) {
      severityParts.push(`${bySeverity.critical} critical`);
    }
    if (bySeverity.error > 0) {
      severityParts.push(`${bySeverity.error} error`);
    }
    if (bySeverity.warning > 0) {
      severityParts.push(`${bySeverity.warning} warning`);
    }

    return `Alerts: ${parts.join(', ')}${severityParts.length > 0 ? ` (${severityParts.join(', ')})` : ''}`;
  }

  /**
   * Calculate system health score
   */
  calculateSystemHealthScore(metrics: SystemResourceMetrics): number {
    let score = 100;

    // CPU usage impact
    if (metrics.cpu.usage > 90) score -= 30;
    else if (metrics.cpu.usage > 80) score -= 20;
    else if (metrics.cpu.usage > 70) score -= 10;

    // Memory usage impact
    const memoryUsagePercent = (metrics.memory.used / metrics.memory.total) * 100;
    if (memoryUsagePercent > 90) score -= 25;
    else if (memoryUsagePercent > 80) score -= 15;
    else if (memoryUsagePercent > 70) score -= 5;

    // Disk usage impact
    const maxDiskUsage = Math.max(...metrics.disk.devices.map(d => d.usagePercent));
    if (maxDiskUsage > 90) score -= 20;
    else if (maxDiskUsage > 80) score -= 10;
    else if (maxDiskUsage > 70) score -= 5;

    // Network errors impact
    if (metrics.network.errors > 1000) score -= 15;
    else if (metrics.network.errors > 100) score -= 10;
    else if (metrics.network.errors > 10) score -= 5;

    return Math.max(0, score);
  }

  /**
   * Get recommended alert actions based on severity
   */
  getRecommendedAlertActions(severity: AlertSeverity): AlertAction[] {
    switch (severity) {
      case 'critical':
        return [
          { type: 'pagerduty', config: { urgency: 'high' } },
          { type: 'email', config: { to: 'oncall@company.com' } },
          { type: 'slack', config: { channel: '#alerts-critical' } },
        ];
      case 'error':
        return [
          { type: 'email', config: { to: 'team@company.com' } },
          { type: 'slack', config: { channel: '#alerts' } },
        ];
      case 'warning':
        return [
          { type: 'slack', config: { channel: '#alerts' } },
        ];
      case 'info':
        return [
          { type: 'log', config: { level: 'info' } },
        ];
    }
  }
}
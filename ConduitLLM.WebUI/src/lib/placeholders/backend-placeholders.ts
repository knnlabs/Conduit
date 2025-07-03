/**
 * Placeholder implementations for backend features that are not yet available
 * These provide realistic empty states instead of mock data
 */

import type {
  SecurityEvent,
  SecurityEventsSummary,
  ThreatAnalytics,
  ProviderIncident,
  AudioUsageSummary,
  AudioDailyUsage,
  RequestLog,
} from '@/types/sdk-responses';

/**
 * Generate empty security events response
 */
export function getEmptySecurityEvents(hours: number = 24): SecurityEventsSummary {
  const now = new Date();
  const startTime = new Date(now.getTime() - hours * 60 * 60 * 1000);
  
  return {
    timestamp: now.toISOString(),
    timeRange: {
      start: startTime.toISOString(),
      end: now.toISOString(),
    },
    totalEvents: 0,
    eventsByType: [],
    eventsBySeverity: [],
    events: [],
  };
}

/**
 * Generate empty threat analytics response
 */
export function getEmptyThreatAnalytics(): ThreatAnalytics {
  return {
    timestamp: new Date().toISOString(),
    metrics: {
      totalThreatsToday: 0,
      uniqueThreatsToday: 0,
      blockedIPs: 0,
      complianceScore: 100, // Perfect score when no threats
    },
    topThreats: [],
    threatDistribution: [],
    threatTrend: [],
  };
}

/**
 * Generate empty provider incidents response
 */
export function getEmptyProviderIncidents(providerId: string): ProviderIncident[] {
  return [];
}

/**
 * Generate empty audio usage summary
 */
export function getEmptyAudioUsageSummary(
  startDate: Date,
  endDate: Date
): AudioUsageSummary {
  return {
    startDate: startDate.toISOString(),
    endDate: endDate.toISOString(),
    totalRequests: 0,
    totalCost: 0,
    totalDuration: 0,
    averageLatency: 0,
    transcriptionGrowth: 0,
    ttsGrowth: 0,
    costGrowth: 0,
    topModels: [],
    dailyUsage: generateEmptyDailyUsage(startDate, endDate),
    modelUsage: [],
    languageDistribution: [],
    modelPerformance: [],
  };
}

/**
 * Generate empty daily usage array for date range
 */
function generateEmptyDailyUsage(
  startDate: Date,
  endDate: Date
): AudioDailyUsage[] {
  const days: AudioDailyUsage[] = [];
  const currentDate = new Date(startDate);
  
  while (currentDate <= endDate) {
    days.push({
      date: currentDate.toISOString().split('T')[0],
      requests: 0,
      cost: 0,
      transcriptions: 0,
      ttsGenerations: 0,
      totalMinutes: 0,
    });
    currentDate.setDate(currentDate.getDate() + 1);
  }
  
  return days;
}

/**
 * Generate empty request logs response
 */
export function getEmptyRequestLogs(
  page: number = 1,
  pageSize: number = 20
): {
  items: RequestLog[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
} {
  return {
    items: [],
    totalCount: 0,
    page,
    pageSize,
    totalPages: 0,
  };
}

/**
 * Check if a feature is available
 */
export function isFeatureAvailable(feature: string): boolean {
  const unavailableFeatures = [
    'security-event-reporting',
    'threat-detection',
    'provider-incidents',
    'audio-usage-detailed',
    'realtime-sessions',
    'analytics-export',
  ];
  
  return !unavailableFeatures.includes(feature);
}

/**
 * Get feature availability message
 */
export function getFeatureMessage(feature: string): string {
  const messages: Record<string, string> = {
    'security-event-reporting': 'Security event reporting is coming soon. This will allow tracking of authentication failures, rate limit violations, and suspicious activities.',
    'threat-detection': 'Threat detection analytics will help identify patterns in security events and potential risks.',
    'provider-incidents': 'Provider incident tracking will show historical outages and performance issues.',
    'audio-usage-detailed': 'Detailed audio usage analytics with language distribution and model performance metrics is under development.',
    'realtime-sessions': 'Real-time session monitoring for audio streaming is not yet available.',
    'analytics-export': 'Analytics export functionality is being implemented to support CSV, JSON, and Excel formats.',
  };
  
  return messages[feature] || 'This feature is not yet available.';
}

/**
 * Get a placeholder response for unavailable features
 */
export function getPlaceholderResponse(feature: string, requested: any = {}) {
  return {
    success: false,
    available: false,
    feature,
    message: getFeatureMessage(feature),
    requested,
    timestamp: new Date().toISOString(),
  };
}
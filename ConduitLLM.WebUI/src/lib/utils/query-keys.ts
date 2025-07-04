/**
 * Centralized query key factory for consistent caching
 */

export const queryKeys = {
  // Analytics query keys
  analytics: {
    all: ['analytics'] as const,
    
    // Overview analytics
    overview: (timeRange?: string) => 
      timeRange 
        ? [...queryKeys.analytics.all, 'overview', timeRange] as const
        : [...queryKeys.analytics.all, 'overview'] as const,
    
    // Usage analytics
    usage: {
      all: () => [...queryKeys.analytics.all, 'usage'] as const,
      summary: (timeRange?: string) => 
        timeRange 
          ? [...queryKeys.analytics.usage.all(), 'summary', timeRange] as const
          : [...queryKeys.analytics.usage.all(), 'summary'] as const,
      metrics: (timeRange?: string, filters?: Record<string, unknown>) => {
        const base = [...queryKeys.analytics.usage.all(), 'metrics'];
        if (timeRange) base.push(timeRange);
        if (filters) base.push(JSON.stringify(filters));
        return base as readonly string[];
      },
      volume: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.usage.all(), 'volume', timeRange] as const
          : [...queryKeys.analytics.usage.all(), 'volume'] as const,
      tokens: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.usage.all(), 'tokens', timeRange] as const
          : [...queryKeys.analytics.usage.all(), 'tokens'] as const,
      errors: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.usage.all(), 'errors', timeRange] as const
          : [...queryKeys.analytics.usage.all(), 'errors'] as const,
      latency: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.usage.all(), 'latency', timeRange] as const
          : [...queryKeys.analytics.usage.all(), 'latency'] as const,
      users: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.usage.all(), 'users', timeRange] as const
          : [...queryKeys.analytics.usage.all(), 'users'] as const,
      endpoints: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.usage.all(), 'endpoints', timeRange] as const
          : [...queryKeys.analytics.usage.all(), 'endpoints'] as const,
    },
    
    // Virtual key analytics
    virtualKeys: {
      all: () => [...queryKeys.analytics.all, 'virtual-keys'] as const,
      overview: (timeRange?: string, keyId?: string) => {
        const base = [...queryKeys.analytics.virtualKeys.all(), 'overview'];
        if (timeRange) base.push(timeRange);
        if (keyId) base.push(keyId);
        return base as readonly string[];
      },
      usage: (timeRange?: string, keyId?: string) => {
        const base = [...queryKeys.analytics.virtualKeys.all(), 'usage'];
        if (timeRange) base.push(timeRange);
        if (keyId) base.push(keyId);
        return base as readonly string[];
      },
      budget: (timeRange?: string, keyId?: string) => {
        const base = [...queryKeys.analytics.virtualKeys.all(), 'budget'];
        if (timeRange) base.push(timeRange);
        if (keyId) base.push(keyId);
        return base as readonly string[];
      },
      performance: (timeRange?: string, keyId?: string) => {
        const base = [...queryKeys.analytics.virtualKeys.all(), 'performance'];
        if (timeRange) base.push(timeRange);
        if (keyId) base.push(keyId);
        return base as readonly string[];
      },
      security: (timeRange?: string, keyId?: string) => {
        const base = [...queryKeys.analytics.virtualKeys.all(), 'security'];
        if (timeRange) base.push(timeRange);
        if (keyId) base.push(keyId);
        return base as readonly string[];
      },
      trends: (timeRange?: string, keyId?: string) => {
        const base = [...queryKeys.analytics.virtualKeys.all(), 'trends'];
        if (timeRange) base.push(timeRange);
        if (keyId) base.push(keyId);
        return base as readonly string[];
      },
      leaderboard: (timeRange?: string, metric?: string) => {
        const base = [...queryKeys.analytics.virtualKeys.all(), 'leaderboard'];
        if (timeRange) base.push(timeRange);
        if (metric) base.push(metric);
        return base as readonly string[];
      },
      comparison: (keyIds?: string[]) => 
        keyIds 
          ? [...queryKeys.analytics.virtualKeys.all(), 'comparison', keyIds] as const
          : [...queryKeys.analytics.virtualKeys.all(), 'comparison'] as const,
    },
    
    // Cost analytics
    costs: {
      all: () => [...queryKeys.analytics.all, 'costs'] as const,
      summary: (timeRange?: string) => 
        timeRange 
          ? [...queryKeys.analytics.costs.all(), 'summary', timeRange] as const
          : [...queryKeys.analytics.costs.all(), 'summary'] as const,
      trends: (timeRange?: string, aggregation?: string) => {
        const base = [...queryKeys.analytics.costs.all(), 'trends'];
        if (timeRange) base.push(timeRange);
        if (aggregation) base.push(aggregation);
        return base as readonly string[];
      },
      byProvider: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.costs.all(), 'by-provider', timeRange] as const
          : [...queryKeys.analytics.costs.all(), 'by-provider'] as const,
      byModel: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.costs.all(), 'by-model', timeRange] as const
          : [...queryKeys.analytics.costs.all(), 'by-model'] as const,
      byVirtualKey: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.costs.all(), 'by-virtual-key', timeRange] as const
          : [...queryKeys.analytics.costs.all(), 'by-virtual-key'] as const,
      byUser: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.costs.all(), 'by-user', timeRange] as const
          : [...queryKeys.analytics.costs.all(), 'by-user'] as const,
      budget: (timeRange?: string) =>
        timeRange 
          ? [...queryKeys.analytics.costs.all(), 'budget', timeRange] as const
          : [...queryKeys.analytics.costs.all(), 'budget'] as const,
      alerts: () =>
        [...queryKeys.analytics.costs.all(), 'alerts'] as const,
      export: (timeRange?: string, format?: string) => {
        const base = [...queryKeys.analytics.costs.all(), 'export'];
        if (timeRange) base.push(timeRange);
        if (format) base.push(format);
        return base as readonly string[];
      },
    },
  },

  // Virtual keys (non-analytics)
  virtualKeys: {
    all: ['virtual-keys'] as const,
    lists: () => [...queryKeys.virtualKeys.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      filters 
        ? [...queryKeys.virtualKeys.lists(), JSON.stringify(filters)] as readonly string[]
        : [...queryKeys.virtualKeys.lists()] as const,
    details: () => [...queryKeys.virtualKeys.all, 'details'] as const,
    detail: (id: string) => [...queryKeys.virtualKeys.details(), id] as const,
  },

  // Providers
  providers: {
    all: ['providers'] as const,
    lists: () => [...queryKeys.providers.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      filters 
        ? [...queryKeys.providers.lists(), JSON.stringify(filters)] as readonly string[]
        : [...queryKeys.providers.lists()] as const,
    details: () => [...queryKeys.providers.all, 'details'] as const,
    detail: (id: string) => [...queryKeys.providers.details(), id] as const,
    models: (providerId: string) => [...queryKeys.providers.all, 'models', providerId] as const,
  },

  // Models
  models: {
    all: ['models'] as const,
    lists: () => [...queryKeys.models.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      filters 
        ? [...queryKeys.models.lists(), JSON.stringify(filters)] as readonly string[]
        : [...queryKeys.models.lists()] as const,
    details: () => [...queryKeys.models.all, 'details'] as const,
    detail: (id: string) => [...queryKeys.models.details(), id] as const,
  },

  // Settings
  settings: {
    all: ['settings'] as const,
    general: () => [...queryKeys.settings.all, 'general'] as const,
    security: () => [...queryKeys.settings.all, 'security'] as const,
    notifications: () => [...queryKeys.settings.all, 'notifications'] as const,
    limits: () => [...queryKeys.settings.all, 'limits'] as const,
  },

  // System
  system: {
    all: ['system'] as const,
    health: () => [...queryKeys.system.all, 'health'] as const,
    status: () => [...queryKeys.system.all, 'status'] as const,
    metrics: () => [...queryKeys.system.all, 'metrics'] as const,
    logs: (filters?: Record<string, unknown>) => 
      filters 
        ? [...queryKeys.system.all, 'logs', JSON.stringify(filters)] as readonly string[]
        : [...queryKeys.system.all, 'logs'] as const,
  },
};

// Helper function to invalidate all analytics queries
export const invalidateAnalyticsQueries = (queryClient: { invalidateQueries: (options: { queryKey: readonly string[] }) => void }) => {
  queryClient.invalidateQueries({ queryKey: queryKeys.analytics.all });
};

// Helper function to invalidate specific analytics section
export const invalidateAnalyticsSection = (
  queryClient: { invalidateQueries: (options: { queryKey: readonly string[] }) => void }, 
  section: 'overview' | 'usage' | 'virtualKeys' | 'costs'
) => {
  switch (section) {
    case 'overview':
      queryClient.invalidateQueries({ queryKey: queryKeys.analytics.overview() });
      break;
    case 'usage':
      queryClient.invalidateQueries({ queryKey: queryKeys.analytics.usage.all() });
      break;
    case 'virtualKeys':
      queryClient.invalidateQueries({ queryKey: queryKeys.analytics.virtualKeys.all() });
      break;
    case 'costs':
      queryClient.invalidateQueries({ queryKey: queryKeys.analytics.costs.all() });
      break;
  }
};

// Helper function to invalidate analytics for a specific time range
export const invalidateAnalyticsForTimeRange = (
  queryClient: { invalidateQueries: (options: { predicate: (query: { queryKey: readonly string[] }) => boolean }) => void },
  timeRange: string
) => {
  // Invalidate all queries that include this time range
  queryClient.invalidateQueries({
    predicate: (query: { queryKey: readonly string[] }) => {
      const queryKey = query.queryKey;
      return Array.isArray(queryKey) && 
             queryKey[0] === 'analytics' && 
             queryKey.includes(timeRange);
    }
  });
};
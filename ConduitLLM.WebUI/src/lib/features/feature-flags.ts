/**
 * Feature flags for controlling visibility of incomplete features
 */

export enum Feature {
  // Analytics & Monitoring
  PROVIDER_HEALTH_DASHBOARD = 'provider_health_dashboard',
  VIRTUAL_KEYS_DASHBOARD = 'virtual_keys_dashboard',
  USAGE_ANALYTICS = 'usage_analytics',
  SYSTEM_PERFORMANCE_FULL = 'system_performance_full',
  
  // Configuration
  ROUTING_PERSISTENCE = 'routing_persistence',
  ADVANCED_CACHING = 'advanced_caching',
  
  // Advanced Features
  BATCH_OPERATIONS = 'batch_operations',
  WEBHOOK_SUPPORT = 'webhook_support',
  CUSTOM_MODELS = 'custom_models',
}

// Feature flag configuration
// In production, these should come from environment variables or a config service
const FEATURE_FLAGS: Record<Feature, boolean> = {
  // Disabled features (waiting for backend implementation)
  [Feature.PROVIDER_HEALTH_DASHBOARD]: false,
  [Feature.VIRTUAL_KEYS_DASHBOARD]: false,
  [Feature.USAGE_ANALYTICS]: false,
  [Feature.SYSTEM_PERFORMANCE_FULL]: false,
  [Feature.ROUTING_PERSISTENCE]: false,
  [Feature.ADVANCED_CACHING]: false,
  
  // Beta features
  [Feature.BATCH_OPERATIONS]: true,
  [Feature.WEBHOOK_SUPPORT]: false,
  [Feature.CUSTOM_MODELS]: false,
};

/**
 * Check if a feature is enabled
 */
export function isFeatureEnabled(feature: Feature): boolean {
  // Allow overriding via environment variables
  const envKey = `NEXT_PUBLIC_FEATURE_${feature.toUpperCase()}`;
  const envValue = process.env[envKey];
  
  if (envValue !== undefined) {
    return envValue === 'true' || envValue === '1';
  }
  
  return FEATURE_FLAGS[feature] ?? false;
}

/**
 * Get all enabled features
 */
export function getEnabledFeatures(): Feature[] {
  return Object.entries(FEATURE_FLAGS)
    .filter(([, enabled]) => enabled)
    .map(([feature]) => feature as Feature);
}

/**
 * Get feature status for display
 */
export function getFeatureStatus(feature: Feature): 'enabled' | 'disabled' | 'beta' {
  if (!isFeatureEnabled(feature)) {
    return 'disabled';
  }
  
  // Mark certain features as beta
  const betaFeatures = [
    Feature.BATCH_OPERATIONS,
    Feature.WEBHOOK_SUPPORT,
    Feature.CUSTOM_MODELS,
  ];
  
  return betaFeatures.includes(feature) ? 'beta' : 'enabled';
}

/**
 * Hook for using feature flags in components
 */
export function useFeature(feature: Feature): boolean {
  return isFeatureEnabled(feature);
}
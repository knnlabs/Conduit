'use client';

import { useQuery } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { BackendErrorHandler } from '@/lib/errors/BackendErrorHandler';

// Query key factory for Feature Availability
export const featureAvailabilityKeys = {
  all: ['feature-availability'] as const,
  features: () => [...featureAvailabilityKeys.all, 'features'] as const,
  feature: (featureName: string) => [...featureAvailabilityKeys.all, 'feature', featureName] as const,
} as const;

export interface FeatureInfo {
  available: boolean;
  status: 'available' | 'coming_soon' | 'in_development' | 'not_planned';
  message?: string;
  version?: string;
  releaseDate?: string;
}

/**
 * Hook to fetch all feature availability
 */
export function useFeatureAvailability() {
  return useQuery({
    queryKey: featureAvailabilityKeys.features(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        return await client.system.getFeatureAvailability();
      } catch (error) {
        // If the endpoint doesn't exist yet, return a fallback
        if (error && typeof error === 'object' && 'status' in error && error.status === 404) {
          // Return hardcoded fallback for now
          return {
            features: {
              'security-event-reporting': {
                available: false,
                status: 'in_development' as const,
                message: 'Security event reporting is coming soon. This will allow tracking of authentication failures, rate limit violations, and suspicious activities.',
              },
              'threat-detection': {
                available: false,
                status: 'in_development' as const,
                message: 'Threat detection analytics will help identify patterns in security events and potential risks.',
              },
              'provider-incidents': {
                available: false,
                status: 'coming_soon' as const,
                message: 'Provider incident tracking will show historical outages and performance issues.',
              },
              'audio-usage-detailed': {
                available: false,
                status: 'in_development' as const,
                message: 'Detailed audio usage analytics with language distribution and model performance metrics is under development.',
              },
              'realtime-sessions': {
                available: false,
                status: 'not_planned' as const,
                message: 'Real-time session monitoring for audio streaming is not yet available.',
              },
              'analytics-export': {
                available: false,
                status: 'coming_soon' as const,
                message: 'Analytics export functionality is being implemented to support CSV, JSON, and Excel formats.',
              },
            },
            timestamp: new Date().toISOString(),
          };
        }
        
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch feature availability');
        throw backendError;
      }
    },
    staleTime: 300000, // 5 minutes
    gcTime: 600000, // 10 minutes
  });
}

/**
 * Hook to check if a specific feature is available
 */
export function useIsFeatureAvailable(featureName: string) {
  const { data, isLoading, error } = useFeatureAvailability();
  
  return {
    isAvailable: data?.features[featureName]?.available ?? false,
    isChecking: isLoading,
    featureInfo: data?.features[featureName],
    error,
  };
}

/**
 * Hook to get feature message
 */
export function useFeatureMessage(featureName: string): string {
  const { featureInfo } = useIsFeatureAvailable(featureName);
  return featureInfo?.message || 'This feature is not yet available.';
}
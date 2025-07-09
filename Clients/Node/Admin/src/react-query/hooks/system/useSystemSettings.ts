import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { GlobalSettingDto, SettingFilters } from '../../../models/settings';

export interface UseSystemSettingsOptions extends UseQueryOptions<GlobalSettingDto[], Error> {
  filters?: SettingFilters;
}

/**
 * Hook to fetch system settings
 * 
 * @param options - Query options and filters
 * @returns Query result with system settings
 * 
 * @example
 * ```tsx
 * // Get all system settings
 * const { data: settings } = useSystemSettings();
 * 
 * // Get settings by category
 * const { data: performanceSettings } = useSystemSettings({
 *   filters: { category: 'Performance' }
 * });
 * 
 * // Get settings with custom stale time
 * const { data: settings } = useSystemSettings({
 *   staleTime: 60000, // 1 minute
 * });
 * ```
 */
export function useSystemSettings(options?: UseSystemSettingsOptions) {
  const { adminClient } = useConduitAdmin();
  const { filters, ...queryOptions } = options || {};
  
  return useQuery<GlobalSettingDto[], Error>({
    queryKey: adminQueryKeys.system.settings(filters),
    queryFn: () => adminClient.settings.getGlobalSettings(filters),
    staleTime: 5 * 60 * 1000, // 5 minutes default
    ...queryOptions,
  });
}

/**
 * Hook to fetch a specific system setting by key
 * 
 * @param key - The setting key
 * @param options - Query options
 * @returns Query result with the setting value
 * 
 * @example
 * ```tsx
 * const { data: setting } = useSystemSetting('MaxConcurrentRequests');
 * ```
 */
export function useSystemSetting(
  key: string,
  options?: UseQueryOptions<GlobalSettingDto | null, Error>
) {
  const { adminClient } = useConduitAdmin();
  
  return useQuery<GlobalSettingDto | null, Error>({
    queryKey: adminQueryKeys.system.setting(key),
    queryFn: () => adminClient.settings.getGlobalSetting(key),
    enabled: !!key,
    staleTime: 5 * 60 * 1000, // 5 minutes default
    ...options,
  });
}

/**
 * Hook to fetch system settings by category
 * 
 * @param category - The settings category
 * @param options - Query options
 * @returns Query result with settings in the category
 * 
 * @example
 * ```tsx
 * const { data: securitySettings } = useSystemSettingsByCategory('Security');
 * ```
 */
export function useSystemSettingsByCategory(
  category: string,
  options?: UseQueryOptions<GlobalSettingDto[], Error>
) {
  const { adminClient } = useConduitAdmin();
  
  return useQuery<GlobalSettingDto[], Error>({
    queryKey: adminQueryKeys.system.settingsByCategory(category),
    queryFn: () => adminClient.settings.getSettingsByCategory(category),
    enabled: !!category,
    staleTime: 5 * 60 * 1000, // 5 minutes default
    ...options,
  });
}
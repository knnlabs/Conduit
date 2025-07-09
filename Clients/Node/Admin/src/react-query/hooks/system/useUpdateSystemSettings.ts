import { useMutation, useQueryClient, UseMutationOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { GlobalSettingDto, UpdateGlobalSettingDto, CreateGlobalSettingDto } from '../../../models/settings';

interface UpdateSettingVariables {
  key: string;
  request: UpdateGlobalSettingDto;
}

interface CreateSettingVariables {
  request: CreateGlobalSettingDto;
}

interface SetSettingVariables {
  key: string;
  value: string;
  description?: string;
  category?: string;
  isSecret?: boolean;
}

/**
 * Hook to update an existing system setting
 * 
 * @param options - Mutation options
 * @returns Mutation object for updating a setting
 * 
 * @example
 * ```tsx
 * const updateSetting = useUpdateSystemSetting();
 * 
 * const handleUpdate = async () => {
 *   await updateSetting.mutateAsync({
 *     key: 'MaxConcurrentRequests',
 *     request: {
 *       value: '200',
 *       description: 'Updated maximum concurrent requests'
 *     }
 *   });
 * };
 * ```
 */
export function useUpdateSystemSetting(
  options?: UseMutationOptions<GlobalSettingDto, Error, UpdateSettingVariables>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<GlobalSettingDto, Error, UpdateSettingVariables>({
    mutationFn: ({ key, request }: UpdateSettingVariables) => adminClient.settings.updateGlobalSetting(key, request),
    onSuccess: (data: GlobalSettingDto, variables: UpdateSettingVariables) => {
      // Invalidate all settings queries
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.settings() });
      // Invalidate specific setting query
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.setting(variables.key) });
      // Invalidate category queries if category is known
      if (data.category) {
        queryClient.invalidateQueries({ 
          queryKey: adminQueryKeys.system.settingsByCategory(data.category) 
        });
      }
    },
    ...options,
  });
}

/**
 * Hook to create a new system setting
 * 
 * @param options - Mutation options
 * @returns Mutation object for creating a setting
 * 
 * @example
 * ```tsx
 * const createSetting = useCreateSystemSetting();
 * 
 * const handleCreate = async () => {
 *   await createSetting.mutateAsync({
 *     request: {
 *       key: 'NewFeatureEnabled',
 *       value: 'true',
 *       category: 'Features',
 *       description: 'Enable new feature'
 *     }
 *   });
 * };
 * ```
 */
export function useCreateSystemSetting(
  options?: UseMutationOptions<GlobalSettingDto, Error, CreateSettingVariables>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<GlobalSettingDto, Error, CreateSettingVariables>({
    mutationFn: ({ request }: CreateSettingVariables) => adminClient.settings.createGlobalSetting(request),
    onSuccess: (data: GlobalSettingDto) => {
      // Invalidate all settings queries
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.settings() });
      // Invalidate category queries if category is known
      if (data.category) {
        queryClient.invalidateQueries({ 
          queryKey: adminQueryKeys.system.settingsByCategory(data.category) 
        });
      }
    },
    ...options,
  });
}

/**
 * Hook to delete a system setting
 * 
 * @param options - Mutation options
 * @returns Mutation object for deleting a setting
 * 
 * @example
 * ```tsx
 * const deleteSetting = useDeleteSystemSetting();
 * 
 * const handleDelete = async (key: string) => {
 *   await deleteSetting.mutateAsync(key);
 * };
 * ```
 */
export function useDeleteSystemSetting(
  options?: UseMutationOptions<void, Error, string>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<void, Error, string>({
    mutationFn: (key: string) => adminClient.settings.deleteGlobalSetting(key),
    onSuccess: (_data: void, key: string) => {
      // Invalidate all settings queries
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.settings() });
      // Invalidate specific setting query
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.setting(key) });
      // Note: We can't invalidate category queries without knowing the category
    },
    ...options,
  });
}

/**
 * Hook to set or update a system setting (convenience method)
 * This will create the setting if it doesn't exist, or update it if it does
 * 
 * @param options - Mutation options
 * @returns Mutation object for setting a value
 * 
 * @example
 * ```tsx
 * const setSetting = useSetSystemSetting();
 * 
 * const handleSet = async () => {
 *   await setSetting.mutateAsync({
 *     key: 'EnableLogging',
 *     value: 'true',
 *     description: 'Enable system logging',
 *     category: 'General'
 *   });
 * };
 * ```
 */
export function useSetSystemSetting(
  options?: UseMutationOptions<void, Error, SetSettingVariables>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<void, Error, SetSettingVariables>({
    mutationFn: async ({ key, value, description, category, isSecret }: SetSettingVariables) => {
      await adminClient.settings.setSetting(key, value, { description, category, isSecret });
    },
    onSuccess: (_data: void, variables: SetSettingVariables) => {
      // Invalidate all settings queries
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.settings() });
      // Invalidate specific setting query
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.setting(variables.key) });
      // Invalidate category queries if category is known
      if (variables.category) {
        queryClient.invalidateQueries({ 
          queryKey: adminQueryKeys.system.settingsByCategory(variables.category) 
        });
      }
    },
    ...options,
  });
}

/**
 * Hook to update multiple system settings in a batch
 * 
 * @param options - Mutation options
 * @returns Mutation object for batch updating settings
 * 
 * @example
 * ```tsx
 * const updateSettings = useUpdateSystemSettings();
 * 
 * const handleBatchUpdate = async () => {
 *   await updateSettings.mutateAsync([
 *     { key: 'Setting1', value: 'value1' },
 *     { key: 'Setting2', value: 'value2' }
 *   ]);
 * };
 * ```
 */
export function useUpdateSystemSettings(
  options?: UseMutationOptions<void, Error, SetSettingVariables[]>
) {
  const { adminClient } = useConduitAdmin();
  const queryClient = useQueryClient();
  
  return useMutation<void, Error, SetSettingVariables[]>({
    mutationFn: async (settings: SetSettingVariables[]) => {
      // Update each setting sequentially
      for (const setting of settings) {
        await adminClient.settings.setSetting(
          setting.key, 
          setting.value, 
          {
            description: setting.description,
            category: setting.category,
            isSecret: setting.isSecret
          }
        );
      }
    },
    onSuccess: () => {
      // Invalidate all settings queries since multiple were updated
      queryClient.invalidateQueries({ queryKey: adminQueryKeys.system.all() });
    },
    ...options,
  });
}
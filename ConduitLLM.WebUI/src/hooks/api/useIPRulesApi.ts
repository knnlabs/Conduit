import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { getAdminClient } from '@/lib/clients/conduit';
import { BackendErrorHandler } from '@/lib/errors/BackendErrorHandler';

export interface IPRule {
  id: string;
  ipAddress: string;
  action: 'allow' | 'block';
  type: 'temporary' | 'permanent';
  reason: string;
  createdAt: string;
  expiresAt?: string;
  isActive: boolean;
  createdBy?: string;
  modifiedAt?: string;
}

export interface IPRuleFormData {
  ipAddress: string;
  action: 'allow' | 'block';
  type: 'temporary' | 'permanent';
  reason: string;
  expiresAt?: string;
}

export interface IPRulesResponse {
  items: IPRule[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

const ipRulesKeys = {
  all: ['ip-rules'] as const,
  lists: () => [...ipRulesKeys.all, 'list'] as const,
  list: (filters?: Record<string, unknown>) => [...ipRulesKeys.lists(), filters] as const,
  details: () => [...ipRulesKeys.all, 'detail'] as const,
  detail: (id: string) => [...ipRulesKeys.details(), id] as const,
};

export function useIPRules(filters?: {
  status?: string;
  type?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: ipRulesKeys.list(filters),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const rules = await client.ipFilters.list({
          filterType: filters?.type as 'whitelist' | 'blacklist' | undefined,
          isEnabled: filters?.status === 'active' ? true : filters?.status === 'inactive' ? false : undefined,
        });
        
        // Transform SDK response to match expected format
        const startIndex = ((filters?.page || 1) - 1) * (filters?.pageSize || 10);
        const endIndex = startIndex + (filters?.pageSize || 10);
        const paginatedRules = rules.slice(startIndex, endIndex);
        
        const transformedResponse: IPRulesResponse = {
          items: paginatedRules.map(rule => ({
            id: rule.id.toString(),
            ipAddress: rule.ipAddressOrCidr,
            action: rule.filterType === 'whitelist' ? 'allow' : 'block',
            type: rule.expiresAt ? 'temporary' : 'permanent',
            reason: rule.description || '',
            createdAt: rule.createdAt,
            expiresAt: rule.expiresAt,
            isActive: rule.isEnabled,
            createdBy: undefined,
            modifiedAt: rule.updatedAt,
          })),
          totalCount: rules.length,
          pageNumber: filters?.page || 1,
          pageSize: filters?.pageSize || 10,
          totalPages: Math.ceil(rules.length / (filters?.pageSize || 10)),
        };
        
        return transformedResponse;
      } catch (error) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
  });
}

export function useCreateIPRule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: IPRuleFormData) => {
      try {
        const client = getAdminClient();
        
        if (data.type === 'temporary' && data.expiresAt) {
          // Create temporary rule
          return await client.ipFilters.createTemporary({
            name: `${data.action}-${data.ipAddress}`,
            ipAddressOrCidr: data.ipAddress,
            filterType: data.action === 'allow' ? 'whitelist' : 'blacklist',
            expiresAt: data.expiresAt,
            reason: data.reason,
            isEnabled: true,
          });
        } else {
          // Create permanent rule
          return await client.ipFilters.create({
            name: `${data.action}-${data.ipAddress}`,
            ipAddressOrCidr: data.ipAddress,
            filterType: data.action === 'allow' ? 'whitelist' : 'blacklist',
            description: data.reason,
            isEnabled: true,
          });
        }
      } catch (error) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ipRulesKeys.lists() });
      notifications.show({
        title: 'IP Rule Created',
        message: 'The IP rule has been created successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const backendError = BackendErrorHandler.classifyError(error);
      const message = BackendErrorHandler.getUserFriendlyMessage(backendError);
      notifications.show({
        title: 'Failed to Create IP Rule',
        message,
        color: 'red',
      });
    },
  });
}

export function useUpdateIPRule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, data }: { id: string; data: Partial<IPRuleFormData> }) => {
      try {
        const client = getAdminClient();
        const updateData: {
          id: number;
          ipAddressOrCidr?: string;
          filterType?: 'whitelist' | 'blacklist';
          description?: string;
          expiresAt?: string;
        } = {
          id: parseInt(id),
        };
        
        if (data.ipAddress) updateData.ipAddressOrCidr = data.ipAddress;
        if (data.action) updateData.filterType = data.action === 'allow' ? 'whitelist' : 'blacklist';
        if (data.reason) updateData.description = data.reason;
        if (data.expiresAt) updateData.expiresAt = data.expiresAt;
        
        return await client.ipFilters.update(parseInt(id), updateData);
      } catch (error) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: ipRulesKeys.lists() });
      queryClient.invalidateQueries({ queryKey: ipRulesKeys.detail(id) });
      notifications.show({
        title: 'IP Rule Updated',
        message: 'The IP rule has been updated successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const backendError = BackendErrorHandler.classifyError(error);
      const message = BackendErrorHandler.getUserFriendlyMessage(backendError);
      notifications.show({
        title: 'Failed to Update IP Rule',
        message,
        color: 'red',
      });
    },
  });
}

export function useDeleteIPRule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      try {
        const client = getAdminClient();
        await client.ipFilters.deleteById(parseInt(id));
        return { success: true };
      } catch (error) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ipRulesKeys.lists() });
      notifications.show({
        title: 'IP Rule Deleted',
        message: 'The IP rule has been deleted successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const backendError = BackendErrorHandler.classifyError(error);
      const message = BackendErrorHandler.getUserFriendlyMessage(backendError);
      notifications.show({
        title: 'Failed to Delete IP Rule',
        message,
        color: 'red',
      });
    },
  });
}
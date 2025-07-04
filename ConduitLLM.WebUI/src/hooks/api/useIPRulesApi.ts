import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { apiFetch } from '@/lib/utils/fetch-wrapper';

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
      const params = new URLSearchParams();
      if (filters?.status) params.append('status', filters.status);
      if (filters?.type) params.append('type', filters.type);
      if (filters?.page) params.append('page', filters.page.toString());
      if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString());

      const response = await apiFetch(`/api/admin/security/ip-rules?${params}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to fetch IP rules');
      }

      return response.json() as Promise<IPRulesResponse>;
    },
  });
}

export function useCreateIPRule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: IPRuleFormData) => {
      const response = await apiFetch('/api/admin/security/ip-rules', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to create IP rule');
      }

      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ipRulesKeys.lists() });
      notifications.show({
        title: 'IP Rule Created',
        message: 'The IP rule has been created successfully',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Failed to Create IP Rule',
        message: error.message,
        color: 'red',
      });
    },
  });
}

export function useUpdateIPRule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, data }: { id: string; data: Partial<IPRuleFormData> }) => {
      const response = await apiFetch(`/api/admin/security/ip-rules/${id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to update IP rule');
      }

      return response.json();
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
    onError: (error: Error) => {
      notifications.show({
        title: 'Failed to Update IP Rule',
        message: error.message,
        color: 'red',
      });
    },
  });
}

export function useDeleteIPRule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      const response = await apiFetch(`/api/admin/security/ip-rules/${id}`, {
        method: 'DELETE',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to delete IP rule');
      }

      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ipRulesKeys.lists() });
      notifications.show({
        title: 'IP Rule Deleted',
        message: 'The IP rule has been deleted successfully',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Failed to Delete IP Rule',
        message: error.message,
        color: 'red',
      });
    },
  });
}
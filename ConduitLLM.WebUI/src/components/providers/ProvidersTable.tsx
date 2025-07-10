'use client';

import {
  Group,
  Text,
  Badge,
  Indicator,
  Stack,
} from '@mantine/core';
import {
  IconTestPipe,
  IconRefresh,
  IconCircleCheck,
  IconCircleX,
  IconClock,
} from '@tabler/icons-react';
import { useProviders, useDeleteProvider } from '@/hooks/useConduitAdmin';
import { BaseTable, type ColumnDef, type ActionDef, type DeleteConfirmation } from '@/components/common/BaseTable';
import { useTableData, tableFormatters } from '@/hooks/useTableData';
import { badgeHelpers } from '@/lib/utils/ui-helpers';

interface Provider {
  id: string;
  providerName: string;
  providerType?: string;
  isEnabled: boolean;
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  modelsAvailable: number;
  createdAt: string;
  apiEndpoint?: string;
  description?: string;
  organizationId?: string;
}

interface ProvidersTableProps {
  onEdit?: (provider: Provider) => void;
  onTest?: (provider: Provider) => void;
  data?: Provider[];
}

export function ProvidersTable({ onEdit, onTest, data }: ProvidersTableProps) {
  const queryResult = useProviders();
  const deleteProvider = useDeleteProvider();
  
  const { handleRefresh, handleDelete } = useTableData({
    queryResult,
    deleteMutation: deleteProvider,
    refreshMessage: 'Provider list refreshed',
    deleteSuccessMessage: 'Provider deleted successfully',
    deleteErrorMessage: 'Failed to delete provider',
  });

  // Use provided data or default to fetched data
  const providers = data || queryResult.data || [];

  const getHealthStatusIcon = (status: string) => {
    switch (status) {
      case 'healthy':
        return <IconCircleCheck size={16} color="green" />;
      case 'unhealthy':
        return <IconCircleX size={16} color="red" />;
      default:
        return <IconClock size={16} color="gray" />;
    }
  };

  const columns: ColumnDef<Provider>[] = [
    {
      key: 'provider',
      label: 'Provider',
      render: (provider) => (
        <Stack gap={4}>
          <Group gap="xs">
            <Text fw={500}>{provider.providerName}</Text>
            <Indicator 
              color={badgeHelpers.getHealthColor(provider.healthStatus)} 
              size={8}
            />
          </Group>
          {provider.description && (
            <Text size="xs" c="dimmed" lineClamp={1}>
              {provider.description}
            </Text>
          )}
        </Stack>
      ),
    },
    {
      key: 'status',
      label: 'Status',
      render: (provider) => (
        <Badge 
          color={badgeHelpers.getStatusColor(provider.isEnabled)} 
          variant={provider.isEnabled ? 'light' : 'filled'}
        >
          {badgeHelpers.formatStatus(provider.isEnabled, { activeText: 'Enabled', inactiveText: 'Disabled' })}
        </Badge>
      ),
    },
    {
      key: 'health',
      label: 'Health',
      render: (provider) => (
        <Group gap="xs">
          {getHealthStatusIcon(provider.healthStatus)}
          <Text size="sm">{badgeHelpers.getStatusConfig(provider.healthStatus, 'health').label}</Text>
        </Group>
      ),
    },
    {
      key: 'models',
      label: 'Models',
      render: (provider) => (
        <Text size="sm">{provider.modelsAvailable}</Text>
      ),
    },
    {
      key: 'lastCheck',
      label: 'Last Check',
      render: (provider) => (
        <Text size="sm" c="dimmed">
          {provider.lastHealthCheck ? tableFormatters.date(provider.lastHealthCheck) : 'Never'}
        </Text>
      ),
    },
    {
      key: 'created',
      label: 'Created',
      render: (provider) => (
        <Text size="sm" c="dimmed">
          {tableFormatters.date(provider.createdAt)}
        </Text>
      ),
    },
  ];

  const customActions: ActionDef<Provider>[] = [
    {
      label: 'Test connection',
      icon: IconTestPipe,
      onClick: (provider) => onTest?.(provider),
      color: 'blue',
    },
    {
      label: 'Refresh models',
      icon: IconRefresh,
      onClick: () => handleRefresh(),
      color: 'blue',
    },
  ];

  const deleteConfirmation: DeleteConfirmation<Provider> = {
    title: 'Delete Provider',
    message: (provider) => 
      `Are you sure you want to delete the provider "${provider.providerName}"? This action cannot be undone and will remove all associated model mappings.`,
  };

  return (
    <BaseTable
      data={providers}
      isLoading={queryResult.isLoading}
      error={queryResult.error}
      columns={columns}
      onEdit={onEdit}
      onDelete={(provider) => handleDelete(provider.id)}
      onRefresh={handleRefresh}
      customActions={customActions}
      deleteConfirmation={deleteConfirmation}
      emptyMessage="No providers configured. Add your first provider to get started."
      minWidth={800}
    />
  );
}
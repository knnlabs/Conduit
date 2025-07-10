'use client';

import {
  Group,
  Text,
  ActionIcon,
  Badge,
  Progress,
  Tooltip,
  Stack,
} from '@mantine/core';
import {
  IconEye,
  IconCopy,
} from '@tabler/icons-react';
import { useVirtualKeys, useDeleteVirtualKey } from '@/hooks/useConduitAdmin';
import { notifications } from '@mantine/notifications';
import { useConnectionStore } from '@/stores/useConnectionStore';
import { useState, useEffect } from 'react';
import { BaseTable, type ColumnDef, type ActionDef, type DeleteConfirmation } from '@/components/common/BaseTable';
import { useTableData, tableFormatters } from '@/hooks/useTableData';
import { badgeHelpers } from '@/lib/utils/ui-helpers';
import { StatusIndicator } from '@/components/common/StatusIndicator';

interface VirtualKey {
  id: string;
  keyName: string;
  keyHash: string;
  currentSpend: number;
  maxBudget?: number;
  isEnabled: boolean;
  createdAt: string;
  lastUsed?: string;
  requestCount: number;
}

interface VirtualKeysTableProps {
  onEdit?: (key: VirtualKey) => void;
  onView?: (key: VirtualKey) => void;
  data?: VirtualKey[];
}

export function VirtualKeysTable({ onEdit, onView, data }: VirtualKeysTableProps) {
  const queryResult = useVirtualKeys();
  const deleteVirtualKey = useDeleteVirtualKey();
  const { status } = useConnectionStore();
  const _signalRStatus = status.signalR;
  const [updatedKeys, setUpdatedKeys] = useState<Set<string>>(new Set());
  
  const { handleRefresh, handleDelete } = useTableData({
    queryResult,
    deleteMutation: deleteVirtualKey,
    refreshMessage: 'Virtual keys refreshed',
    deleteSuccessMessage: 'Virtual key deleted successfully',
    deleteErrorMessage: 'Failed to delete virtual key',
  });

  // Use provided data or default to fetched data
  const virtualKeys = data || queryResult.data || [];

  // Show update indicator when keys are updated via SignalR
  useEffect(() => {
    if (updatedKeys.size > 0) {
      const timer = setTimeout(() => {
        setUpdatedKeys(new Set());
      }, 2000);
      return () => clearTimeout(timer);
    }
    return undefined;
  }, [updatedKeys]);

  const handleCopyKey = (keyHash: string) => {
    navigator.clipboard.writeText(keyHash);
    notifications.show({
      title: 'Copied',
      message: 'Key hash copied to clipboard',
      color: 'green',
    });
  };

  const getBudgetUsagePercentage = (currentSpend: number, maxBudget?: number) => {
    if (!maxBudget) return 0;
    return Math.min((currentSpend / maxBudget) * 100, 100);
  };

  // Helper to get key hash from various possible properties
  const getKeyHash = (key: VirtualKey) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return (key as any).keyHash || (key as any).keyPrefix || (key as any).apiKey || 'N/A';
  };

  const columns: ColumnDef<VirtualKey>[] = [
    {
      key: 'keyName',
      label: 'Name',
      sortable: true,
      filterable: true,
      accessor: 'keyName',
      render: (key) => (
        <Group gap="sm">
          <div>
            <Text fw={500}>{key.keyName}</Text>
            <Text size="xs" c="dimmed">
              ID: {key.id}
            </Text>
          </div>
        </Group>
      ),
    },
    {
      key: 'keyHash',
      label: 'Key Hash',
      render: (key) => (
        <Group gap="xs">
          <Text size="sm" style={{ fontFamily: 'monospace' }}>
            {getKeyHash(key).substring(0, 12)}...
          </Text>
          <ActionIcon
            variant="subtle"
            size="sm"
            onClick={() => handleCopyKey(getKeyHash(key))}
          >
            <IconCopy size={14} />
          </ActionIcon>
        </Group>
      ),
    },
    {
      key: 'isEnabled',
      label: 'Status',
      sortable: true,
      filterable: true,
      accessor: 'isEnabled',
      render: (key) => (
        <StatusIndicator
          status={key.isEnabled}
          variant="badge"
          size="sm"
        />
      ),
    },
    {
      key: 'currentSpend',
      label: 'Spend / Budget',
      sortable: true,
      sortType: 'currency',
      accessor: 'currentSpend',
      render: (key) => {
        const budgetUsagePercentage = getBudgetUsagePercentage(key.currentSpend, key.maxBudget);
        const budgetUsageColor = key.maxBudget 
          ? badgeHelpers.getPercentageColor(budgetUsagePercentage, { danger: 90, warning: 75, good: 0 })
          : 'blue';

        return (
          <Stack gap={4}>
            <Group justify="space-between">
              <Text size="sm" fw={500}>
                {tableFormatters.currency(key.currentSpend)}
              </Text>
              {key.maxBudget && (
                <Text size="xs" c="dimmed">
                  / {tableFormatters.currency(key.maxBudget)}
                </Text>
              )}
            </Group>
            {key.maxBudget && (
              <Progress
                value={budgetUsagePercentage}
                color={budgetUsageColor}
                size="xs"
              />
            )}
          </Stack>
        );
      },
    },
    {
      key: 'requests',
      label: 'Requests',
      render: (key) => (
        <Text size="sm">{tableFormatters.number(key.requestCount)}</Text>
      ),
    },
    {
      key: 'lastUsed',
      label: 'Last Used',
      sortable: true,
      sortType: 'date',
      accessor: (key) => (key as any).lastUsed || (key as any).lastUsedAt,
      render: (key) => (
        <Text size="sm" c="dimmed">
          {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
          {tableFormatters.date((key as any).lastUsed || (key as any).lastUsedAt)}
        </Text>
      ),
    },
  ];

  const customActions: ActionDef<VirtualKey>[] = [
    {
      label: 'View details',
      icon: IconEye,
      onClick: (key) => onView?.(key),
      color: 'gray',
    },
    {
      label: 'Copy key',
      icon: IconCopy,
      onClick: (key) => handleCopyKey(getKeyHash(key)),
      color: 'blue',
    },
  ];

  const deleteConfirmation: DeleteConfirmation<VirtualKey> = {
    title: 'Delete Virtual Key',
    message: (key) => 
      `Are you sure you want to delete the virtual key "${key.keyName}"? This action cannot be undone and will immediately revoke access for this key.`,
  };

  return (
    <BaseTable
      data={virtualKeys}
      isLoading={queryResult.isLoading}
      error={queryResult.error}
      columns={columns}
      searchable
      searchPlaceholder="Search virtual keys by name..."
      onEdit={onEdit}
      onDelete={(key) => handleDelete(key.id)}
      onRefresh={handleRefresh}
      customActions={customActions}
      deleteConfirmation={deleteConfirmation}
      emptyMessage="No virtual keys found. Create your first virtual key to get started."
      minWidth={800}
    />
  );
}
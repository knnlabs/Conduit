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
<<<<<<< HEAD
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';
=======
import { useVirtualKeys, useDeleteVirtualKey } from '@/hooks/useConduitAdmin';
import { notifications } from '@mantine/notifications';
import { useConnectionStore } from '@/stores/useConnectionStore';
import { useState, useEffect } from 'react';
import { BaseTable, type ColumnDef, type ActionDef, type DeleteConfirmation } from '@/components/common/BaseTable';
import { useTableData, tableFormatters } from '@/hooks/useTableData';
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
import { badgeHelpers } from '@/lib/utils/badge-helpers';
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
  onDelete?: (keyId: string) => void;
}

<<<<<<< HEAD
export function VirtualKeysTable({ onEdit, onView, data, onDelete }: VirtualKeysTableProps) {
  const virtualKeys = data || [];
=======
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
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6

  const handleCopyKey = (keyHash: string) => {
    navigator.clipboard.writeText(keyHash);
    notifications.show({
      title: 'Copied',
      message: 'Key hash copied to clipboard',
      color: 'green',
    });
  };

<<<<<<< HEAD
  const handleDelete = (key: VirtualKey) => {
    modals.openConfirmModal({
      title: 'Delete Virtual Key',
      children: (
        <Text size="sm">
          Are you sure you want to delete the virtual key &quot;{key.keyName}&quot;? 
          This action cannot be undone and will immediately revoke access for this key.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => onDelete?.(key.id),
    });
  };


=======
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
  const getBudgetUsagePercentage = (currentSpend: number, maxBudget?: number) => {
    if (!maxBudget) return 0;
    return Math.min((currentSpend / maxBudget) * 100, 100);
  };

  // Helper to get key hash from various possible properties
  const getKeyHash = (key: VirtualKey) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return (key as any).keyHash || (key as any).keyPrefix || (key as any).apiKey || 'N/A';
  };

<<<<<<< HEAD

  const rows = virtualKeys?.map((key: VirtualKey) => {
    const budgetUsagePercentage = getBudgetUsagePercentage(key.currentSpend, key.maxBudget);
    const budgetUsageColor = key.maxBudget 
      ? badgeHelpers.getPercentageColor(budgetUsagePercentage, { danger: 90, warning: 75, good: 0 })
      : 'blue';

    return (
      <Table.Tr key={key.id}>
        <Table.Td>
          <Group gap="sm">
            <div>
              <Text fw={500}>{key.keyName}</Text>
              <Text size="xs" c="dimmed">
                ID: {key.id}
              </Text>
            </div>
          </Group>
        </Table.Td>

        <Table.Td>
          <Group gap="xs">
            <Text size="sm" style={{ fontFamily: 'monospace' }}>
              {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
              {((key as any).keyHash || (key as any).keyPrefix || (key as any).apiKey || 'N/A').substring(0, 12)}...
=======
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
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
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
<<<<<<< HEAD
    <Paper withBorder radius="md">
      <Box pos="relative">
        <Table.ScrollContainer minWidth={800}>
          <Table verticalSpacing="sm" horizontalSpacing="md">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Name</Table.Th>
                <Table.Th>Key Hash</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Spend / Budget</Table.Th>
                <Table.Th>Requests</Table.Th>
                <Table.Th>Last Used</Table.Th>
                <Table.Th></Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>

        {virtualKeys && virtualKeys.length === 0 && (
          <Box p="xl" style={{ textAlign: 'center' }}>
            <Text c="dimmed">No virtual keys found. Create your first virtual key to get started.</Text>
          </Box>
        )}
      </Box>
    </Paper>
=======
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
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
  );
}
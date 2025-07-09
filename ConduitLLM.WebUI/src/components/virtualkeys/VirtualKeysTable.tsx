'use client';

import {
  Table,
  Group,
  Text,
  ActionIcon,
  Badge,
  Menu,
  rem,
  Box,
  Progress,
  Tooltip,
  Paper,
  Stack,
  LoadingOverlay,
  Alert,
  // Removed unused Transition import
} from '@mantine/core';
import {
  IconDots,
  IconEdit,
  IconTrash,
  IconEye,
  IconCopy,
  IconAlertCircle,
  // Removed unused IconActivity import
} from '@tabler/icons-react';
import { useVirtualKeys, useDeleteVirtualKey } from '@/hooks/useConduitAdmin';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { useConnectionStore } from '@/stores/useConnectionStore';
import { useState, useEffect } from 'react';
import { formatters } from '@/lib/utils/formatters';
import { badgeHelpers } from '@/lib/utils/badge-helpers';

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
  const { data: defaultKeys, isLoading, error } = useVirtualKeys();
  const deleteVirtualKey = useDeleteVirtualKey();
  const { status } = useConnectionStore();
  const _signalRStatus = status.signalR;
  const [updatedKeys, setUpdatedKeys] = useState<Set<string>>(new Set());
  
  // Use provided data or default to fetched data
  const virtualKeys = data || defaultKeys;

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
      onConfirm: () => deleteVirtualKey.mutate(key.id),
    });
  };


  const getBudgetUsagePercentage = (currentSpend: number, maxBudget?: number) => {
    if (!maxBudget) return 0;
    return Math.min((currentSpend / maxBudget) * 100, 100);
  };


  if (error) {
    return (
      <Alert 
        icon={<IconAlertCircle size={16} />} 
        title="Error loading virtual keys"
        color="red"
      >
        {error instanceof Error ? error.message : 'Failed to load virtual keys. Please try again.'}
      </Alert>
    );
  }

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
            </Text>
            <ActionIcon
              variant="subtle"
              size="sm"
              onClick={() => {
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                handleCopyKey((key as any).keyHash || (key as any).apiKey || (key as any).keyPrefix || '');
              }}
            >
              <IconCopy size={14} />
            </ActionIcon>
          </Group>
        </Table.Td>

        <Table.Td>
          <Badge 
            color={badgeHelpers.getStatusColor(key.isEnabled)} 
            variant={key.isEnabled ? 'light' : 'filled'}
          >
            {badgeHelpers.formatStatus(key.isEnabled)}
          </Badge>
        </Table.Td>

        <Table.Td>
          <Stack gap={4}>
            <Group justify="space-between">
              <Text size="sm" fw={500}>
                {formatters.currency(key.currentSpend, { precision: 4 })}
              </Text>
              {key.maxBudget && (
                <Text size="xs" c="dimmed">
                  / {formatters.currency(key.maxBudget, { precision: 4 })}
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
        </Table.Td>

        <Table.Td>
          <Text size="sm">{formatters.number(key.requestCount)}</Text>
        </Table.Td>

        <Table.Td>
          <Text size="sm" c="dimmed">
            {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
            {formatters.date((key as any).lastUsed || (key as any).lastUsedAt)}
          </Text>
        </Table.Td>

        <Table.Td>
          <Group gap={0} justify="flex-end">
            <Tooltip label="View details">
              <ActionIcon 
                variant="subtle" 
                color="gray"
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                onClick={() => onView?.(key as any)}
              >
                <IconEye size={16} />
              </ActionIcon>
            </Tooltip>
            
            <Menu shadow="md" width={200}>
              <Menu.Target>
                <ActionIcon variant="subtle" color="gray">
                  <IconDots size={16} />
                </ActionIcon>
              </Menu.Target>

              <Menu.Dropdown>
                <Menu.Item
                  leftSection={<IconEdit style={{ width: rem(14), height: rem(14) }} />}
                  // eslint-disable-next-line @typescript-eslint/no-explicit-any
                  onClick={() => onEdit?.(key as any)}
                >
                  Edit
                </Menu.Item>
                
                <Menu.Item
                  leftSection={<IconCopy style={{ width: rem(14), height: rem(14) }} />}
                  onClick={() => {
                    // eslint-disable-next-line @typescript-eslint/no-explicit-any
                    handleCopyKey((key as any).keyHash || (key as any).apiKey || (key as any).keyPrefix || '');
                  }}
                >
                  Copy key
                </Menu.Item>
                
                <Menu.Divider />
                
                <Menu.Item
                  color="red"
                  leftSection={<IconTrash style={{ width: rem(14), height: rem(14) }} />}
                  // eslint-disable-next-line @typescript-eslint/no-explicit-any
                  onClick={() => handleDelete(key as any)}
                >
                  Delete
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Table.Td>
      </Table.Tr>
    );
  });

  return (
    <Paper withBorder radius="md">
      <Box pos="relative">
        <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
        
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
  );
}
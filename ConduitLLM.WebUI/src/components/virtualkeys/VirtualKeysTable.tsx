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
  Transition,
} from '@mantine/core';
import {
  IconDots,
  IconEdit,
  IconTrash,
  IconEye,
  IconCopy,
  IconAlertCircle,
  IconActivity,
} from '@tabler/icons-react';
import { useVirtualKeys, useDeleteVirtualKey } from '@/hooks/api/useAdminApi';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { useConnectionStore } from '@/stores/useConnectionStore';
import { useState, useEffect } from 'react';

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
  const signalRStatus = status.signalR;
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
          Are you sure you want to delete the virtual key "{key.keyName}"? 
          This action cannot be undone and will immediately revoke access for this key.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => deleteVirtualKey.mutate(key.id),
    });
  };

  const getBudgetUsageColor = (currentSpend: number, maxBudget?: number) => {
    if (!maxBudget) return 'blue';
    const percentage = (currentSpend / maxBudget) * 100;
    if (percentage >= 90) return 'red';
    if (percentage >= 75) return 'orange';
    return 'green';
  };

  const getBudgetUsagePercentage = (currentSpend: number, maxBudget?: number) => {
    if (!maxBudget) return 0;
    return Math.min((currentSpend / maxBudget) * 100, 100);
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 4,
    }).format(amount);
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  if (error) {
    return (
      <Alert 
        icon={<IconAlertCircle size={16} />} 
        title="Error loading virtual keys"
        color="red"
      >
        {error.message || 'Failed to load virtual keys. Please try again.'}
      </Alert>
    );
  }

  const rows = virtualKeys?.map((key: VirtualKey) => {
    const budgetUsagePercentage = getBudgetUsagePercentage(key.currentSpend, key.maxBudget);
    const budgetUsageColor = getBudgetUsageColor(key.currentSpend, key.maxBudget);

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
              {key.keyHash.substring(0, 12)}...
            </Text>
            <ActionIcon
              variant="subtle"
              size="sm"
              onClick={() => handleCopyKey(key.keyHash)}
            >
              <IconCopy size={14} />
            </ActionIcon>
          </Group>
        </Table.Td>

        <Table.Td>
          <Badge 
            color={key.isEnabled ? 'green' : 'red'} 
            variant={key.isEnabled ? 'light' : 'filled'}
          >
            {key.isEnabled ? 'Active' : 'Disabled'}
          </Badge>
        </Table.Td>

        <Table.Td>
          <Stack gap={4}>
            <Group justify="space-between">
              <Text size="sm" fw={500}>
                {formatCurrency(key.currentSpend)}
              </Text>
              {key.maxBudget && (
                <Text size="xs" c="dimmed">
                  / {formatCurrency(key.maxBudget)}
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
          <Text size="sm">{key.requestCount.toLocaleString()}</Text>
        </Table.Td>

        <Table.Td>
          <Text size="sm" c="dimmed">
            {key.lastUsed ? formatDate(key.lastUsed) : 'Never'}
          </Text>
        </Table.Td>

        <Table.Td>
          <Group gap={0} justify="flex-end">
            <Tooltip label="View details">
              <ActionIcon 
                variant="subtle" 
                color="gray"
                onClick={() => onView?.(key)}
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
                  onClick={() => onEdit?.(key)}
                >
                  Edit
                </Menu.Item>
                
                <Menu.Item
                  leftSection={<IconCopy style={{ width: rem(14), height: rem(14) }} />}
                  onClick={() => handleCopyKey(key.keyHash)}
                >
                  Copy key hash
                </Menu.Item>
                
                <Menu.Divider />
                
                <Menu.Item
                  color="red"
                  leftSection={<IconTrash style={{ width: rem(14), height: rem(14) }} />}
                  onClick={() => handleDelete(key)}
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
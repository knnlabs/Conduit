'use client';

import {
  Table,
  Group,
  Text,
  Badge,
  ActionIcon,
  Progress,
  Tooltip,
  Stack,
  Box,
  Paper,
  Menu,
  rem,
} from '@mantine/core';
import {
  IconEye,
  IconCopy,
  IconEdit,
  IconTrash,
  IconDotsVertical,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';
import type { VirtualKeyDto } from '@knn_labs/conduit-admin-client';

// Extend VirtualKeyDto with UI-specific fields added by the API
interface VirtualKeyWithUI extends VirtualKeyDto {
  displayKey: string;
}

interface VirtualKeysTableProps {
  onEdit?: (key: VirtualKeyWithUI) => void;
  onView?: (key: VirtualKeyWithUI) => void;
  data?: VirtualKeyWithUI[];
  onDelete?: (keyId: string) => void;
}

export function VirtualKeysTable({ onEdit, onView, data, onDelete }: VirtualKeysTableProps) {
  const virtualKeys = data || [];

  const handleCopyKey = (keyHash: string) => {
    navigator.clipboard.writeText(keyHash);
    notifications.show({
      title: 'Copied',
      message: 'Key hash copied to clipboard',
      color: 'green',
    });
  };

  const handleDelete = (key: VirtualKeyWithUI) => {
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
      onConfirm: () => onDelete?.(key.id.toString()),
    });
  };

  const getBudgetUsagePercentage = (currentSpend: number, maxBudget?: number) => {
    if (!maxBudget) return 0;
    return Math.min((currentSpend / maxBudget) * 100, 100);
  };

  const getBudgetUsageColor = (percentage: number) => {
    if (percentage >= 90) return 'red';
    if (percentage >= 75) return 'yellow';
    return 'green';
  };

  const rows = virtualKeys.map((key) => {
    const budgetUsagePercentage = getBudgetUsagePercentage(key.currentSpend, key.maxBudget);
    const budgetUsageColor = key.maxBudget ? getBudgetUsageColor(budgetUsagePercentage) : 'blue';

    return (
      <Table.Tr key={key.id}>
        <Table.Td>
          <Stack gap={4}>
            <Text fw={500}>{key.keyName}</Text>
            {key.metadata && (
              <Text size="xs" c="dimmed">{JSON.stringify(key.metadata)}</Text>
            )}
          </Stack>
        </Table.Td>

        <Table.Td>
          <Group gap="xs">
            <Text size="sm" style={{ fontFamily: 'monospace' }}>
              {key.displayKey.substring(0, 12)}...
            </Text>
            <Tooltip label="Copy full key">
              <ActionIcon
                variant="subtle"
                size="xs"
                onClick={() => handleCopyKey(key.displayKey)}
              >
                <IconCopy size={14} />
              </ActionIcon>
            </Tooltip>
          </Group>
        </Table.Td>

        <Table.Td>
          <Stack gap={4}>
            <Text size="sm" fw={500}>
              ${key.currentSpend.toFixed(2)}
              {key.maxBudget && (
                <Text component="span" size="sm" c="dimmed">
                  {' '}/ ${key.maxBudget.toFixed(2)}
                </Text>
              )}
            </Text>
            {key.maxBudget && (
              <Progress
                value={budgetUsagePercentage}
                color={budgetUsageColor}
                size="sm"
                radius="md"
              />
            )}
          </Stack>
        </Table.Td>

        <Table.Td>
          <Text size="sm">{key.requestCount?.toLocaleString() || '0'}</Text>
        </Table.Td>

        <Table.Td>
          <Badge
            color={key.isEnabled ? 'green' : 'gray'}
            variant="light"
            size="sm"
          >
            {key.isEnabled ? 'Active' : 'Inactive'}
          </Badge>
        </Table.Td>

        <Table.Td>
          <Text size="sm" c="dimmed">
            {key.lastUsedAt ? formatters.date(key.lastUsedAt) : 'Never'}
          </Text>
        </Table.Td>

        <Table.Td>
          <Group gap={0} justify="flex-end">
            <Menu position="bottom-end" withinPortal>
              <Menu.Target>
                <ActionIcon variant="subtle" color="gray" size="sm">
                  <IconDotsVertical style={{ width: rem(16), height: rem(16) }} />
                </ActionIcon>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Item
                  leftSection={<IconEye style={{ width: rem(14), height: rem(14) }} />}
                  onClick={() => onView?.(key)}
                >
                  View Details
                </Menu.Item>
                <Menu.Item
                  leftSection={<IconEdit style={{ width: rem(14), height: rem(14) }} />}
                  onClick={() => onEdit?.(key)}
                >
                  Edit
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
        <Table.ScrollContainer minWidth={800}>
          <Table verticalSpacing="sm" horizontalSpacing="md">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Name</Table.Th>
                <Table.Th>Key Hash</Table.Th>
                <Table.Th>Budget Usage</Table.Th>
                <Table.Th>Requests</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Last Used</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>{rows}</Table.Tbody>
          </Table>
        </Table.ScrollContainer>

        {virtualKeys.length === 0 && (
          <Box p="xl" style={{ textAlign: 'center' }}>
            <Text c="dimmed">No virtual keys found. Create your first virtual key to get started.</Text>
          </Box>
        )}
      </Box>
    </Paper>
  );
}
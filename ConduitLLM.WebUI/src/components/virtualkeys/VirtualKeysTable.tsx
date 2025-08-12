'use client';

import {
  Table,
  Group,
  Text,
  Badge,
  ActionIcon,
  Stack,
  Box,
  Paper,
  Menu,
  rem,
  Anchor,
} from '@mantine/core';
import {
  IconEye,
  IconEdit,
  IconTrash,
  IconDotsVertical,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { formatters } from '@/lib/utils/formatters';
import { useRouter } from 'next/navigation';
import type { VirtualKeyDto, VirtualKeyGroupDto } from '@knn_labs/conduit-admin-client';

interface VirtualKeysTableProps {
  onEdit?: (key: VirtualKeyDto) => void;
  onView?: (key: VirtualKeyDto) => void;
  data?: VirtualKeyDto[];
  groups?: VirtualKeyGroupDto[];
  onDelete?: (keyId: string) => void;
}

export function VirtualKeysTable({ onEdit, onView, data, groups, onDelete }: VirtualKeysTableProps) {
  const virtualKeys = data ?? [];
  const router = useRouter();
  
  // Create maps for group data lookup
  const groupNameMap = new Map<number, string>();
  const groupBalanceMap = new Map<number, number>();
  groups?.forEach(group => {
    groupNameMap.set(group.id, group.groupName);
    groupBalanceMap.set(group.id, group.balance);
  });


  const handleDelete = (key: VirtualKeyDto) => {
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

  const handleGroupClick = (groupId: number) => {
    router.push(`/virtualkeys/groups?groupId=${groupId}`);
  };

  const rows = virtualKeys.map((key) => {

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
          <Text size="sm" style={{ fontFamily: 'monospace' }}>
            {key.keyPrefix ?? 'N/A'}
          </Text>
        </Table.Td>

        <Table.Td>
          <Stack gap={4}>
            <Anchor 
              size="sm" 
              fw={500}
              onClick={() => handleGroupClick(key.virtualKeyGroupId)}
              style={{ cursor: 'pointer' }}
            >
              {groupNameMap.get(key.virtualKeyGroupId) ?? `Group ID: ${key.virtualKeyGroupId}`}
            </Anchor>
            <Text size="xs" c="dimmed">
              Balance tracked at group level
            </Text>
          </Stack>
        </Table.Td>

        <Table.Td>
          <Text fw={500} size="sm" style={{ fontFamily: 'monospace' }}>
            {(() => {
              const balance = groupBalanceMap.get(key.virtualKeyGroupId);
              return balance !== undefined ? `$${balance.toFixed(2)}` : 'N/A';
            })()}
          </Text>
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
            {key.expiresAt ? formatters.date(key.expiresAt) : 'No expiration'}
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
                <Table.Th>Key Prefix</Table.Th>
                <Table.Th>Virtual Key Group</Table.Th>
                <Table.Th>Current Balance</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Expires</Table.Th>
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
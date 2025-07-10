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
  Tooltip,
} from '@mantine/core';
import {
  IconDots,
  IconEdit,
  IconTrash,
  IconTestPipe,
  IconArrowRight,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';

interface ModelMappingsTableProps {
  data: any[];
  onEdit?: (mapping: any) => void;
  onTest?: (mappingId: string) => void;
  onDelete?: (mappingId: string) => void;
  testingMappings?: Set<string>;
}

export function ModelMappingsTable({ 
  data,
  onEdit,
  onTest,
  onDelete,
  testingMappings = new Set(),
}: ModelMappingsTableProps) {
  const handleDelete = (mapping: any) => {
    modals.openConfirmModal({
      title: 'Delete Model Mapping',
      children: (
        <Text size="sm">
          Are you sure you want to delete the mapping for model "{mapping.modelName}"?
          This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => onDelete?.(mapping.id),
    });
  };

  if (data.length === 0) {
    return (
      <Box p="md">
        <Text c="dimmed" ta="center">
          No model mappings found. Create your first mapping to get started.
        </Text>
      </Box>
    );
  }

  const rows = data.map((mapping) => (
    <Table.Tr key={mapping.id}>
      <Table.Td>
        <Group gap="xs">
          <Text size="sm" fw={500}>{mapping.modelName}</Text>
          <IconArrowRight size={14} style={{ color: 'var(--mantine-color-dimmed)' }} />
          <Text size="sm" c="dimmed">{mapping.providerModelId}</Text>
        </Group>
      </Table.Td>
      
      <Table.Td>
        <Text size="sm">{mapping.providerName || 'Unknown'}</Text>
      </Table.Td>

      <Table.Td>
        <Badge 
          color={mapping.isEnabled ? 'green' : 'gray'} 
          variant="light"
          size="sm"
        >
          {mapping.isEnabled ? 'Enabled' : 'Disabled'}
        </Badge>
      </Table.Td>

      <Table.Td>
        <Text size="sm">{mapping.priority}</Text>
      </Table.Td>

      <Table.Td>
        <Text size="sm">{mapping.requestCount || 0}</Text>
      </Table.Td>

      <Table.Td>
        <Group gap="xs" justify="flex-end">
          <Tooltip label="Test mapping">
            <ActionIcon
              variant="subtle"
              size="sm"
              onClick={() => onTest?.(mapping.id)}
              loading={testingMappings.has(mapping.id)}
            >
              <IconTestPipe size={16} />
            </ActionIcon>
          </Tooltip>

          <Menu position="bottom-end">
            <Menu.Target>
              <ActionIcon variant="subtle" size="sm">
                <IconDots size={16} />
              </ActionIcon>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Item
                leftSection={<IconEdit style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => onEdit?.(mapping)}
              >
                Edit
              </Menu.Item>
              <Menu.Item
                leftSection={<IconTestPipe style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => onTest?.(mapping.id)}
              >
                Test Connection
              </Menu.Item>
              <Menu.Divider />
              <Menu.Item
                color="red"
                leftSection={<IconTrash style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => handleDelete(mapping)}
              >
                Delete
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>
        </Group>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Box style={{ overflowX: 'auto' }}>
      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Model Mapping</Table.Th>
            <Table.Th>Provider</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Priority</Table.Th>
            <Table.Th>Requests</Table.Th>
            <Table.Th style={{ width: 100 }}></Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>{rows}</Table.Tbody>
      </Table>
    </Box>
  );
}
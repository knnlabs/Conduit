'use client';

import {
  Group,
  Text,
  Badge,
  Menu,
  rem,
  Box,
  Paper,
  Tooltip,
  Stack,
  ActionIcon,
  Table,
} from '@mantine/core';
import {
  IconTestPipe,
  IconRefresh,
  IconCircleCheck,
  IconCircleX,
  IconClock,
  IconEdit,
  IconTrash,
  IconDotsVertical,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { formatters } from '@/lib/utils/formatters';
import { notifications } from '@mantine/notifications';

// Use the same interface as the page component
interface Provider {
  id: string;
  name: string;
  type: string;
  isEnabled: boolean;
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  endpoint?: string;
  supportedModels: string[];
  configuration: Record<string, unknown>;
  createdDate: string;
  modifiedDate: string;
  models?: string[];
}

interface ProvidersTableProps {
  onEdit?: (provider: Provider) => void;
  onTest?: (providerId: string) => void;
  onDelete?: (providerId: string) => void;
  data?: Provider[];
  testingProviders?: Set<string>;
}

export function ProvidersTable({ onEdit, onTest, onDelete, data, testingProviders = new Set() }: ProvidersTableProps) {
  const providers = data || [];

  const handleDelete = (provider: Provider) => {
    modals.openConfirmModal({
      title: 'Delete Provider',
      children: (
        <Text size="sm">
          Are you sure you want to delete {provider.name}? This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => onDelete?.(provider.id),
    });
  };

  const getHealthIcon = (status: string) => {
    switch (status) {
      case 'healthy':
        return <IconCircleCheck size={16} />;
      case 'unhealthy':
        return <IconCircleX size={16} />;
      default:
        return <IconClock size={16} />;
    }
  };

  const getHealthColor = (status: string) => {
    switch (status) {
      case 'healthy':
        return 'green';
      case 'unhealthy':
        return 'red';
      default:
        return 'gray';
    }
  };

  const rows = providers.map((provider) => (
    <Table.Tr key={provider.id}>
      <Table.Td>
        <Stack gap={4}>
          <Text fw={500}>{provider.name}</Text>
          {provider.type && (
            <Text size="xs" c="dimmed">Type: {provider.type}</Text>
          )}
        </Stack>
      </Table.Td>

      <Table.Td>
        <Badge
          color={provider.isEnabled ? 'green' : 'gray'}
          variant="light"
          size="sm"
        >
          {provider.isEnabled ? 'Enabled' : 'Disabled'}
        </Badge>
      </Table.Td>

      <Table.Td>
        <Tooltip label={`Last checked: ${provider.lastHealthCheck ? formatters.date(provider.lastHealthCheck) : 'Never'}`}>
          <Group gap="xs">
            {getHealthIcon(provider.healthStatus)}
            <Text size="sm" c={getHealthColor(provider.healthStatus)}>
              {provider.healthStatus}
            </Text>
          </Group>
        </Tooltip>
      </Table.Td>

      <Table.Td>
        <Group gap={4}>
          {provider.models && provider.models.slice(0, 2).map((model, idx) => (
            <Badge key={idx} size="xs" variant="light">
              {model}
            </Badge>
          ))}
          {provider.models && provider.models.length > 2 && (
            <Badge size="xs" variant="light" color="gray">
              +{provider.models.length - 2}
            </Badge>
          )}
        </Group>
      </Table.Td>

      <Table.Td>
        <Text size="sm" c="dimmed">
          {provider.createdDate ? formatters.date(provider.createdDate) : '-'}
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
                leftSection={<IconEdit style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => onEdit?.(provider)}
              >
                Edit
              </Menu.Item>
              <Menu.Item
                leftSection={<IconTestPipe style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => onTest?.(provider.id)}
                disabled={testingProviders.has(provider.id)}
              >
                {testingProviders.has(provider.id) ? 'Testing...' : 'Test Connection'}
              </Menu.Item>
              <Menu.Divider />
              <Menu.Item
                color="red"
                leftSection={<IconTrash style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => handleDelete(provider)}
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
    <Paper withBorder radius="md">
      <Box pos="relative">
        <Table.ScrollContainer minWidth={800}>
          <Table verticalSpacing="sm" horizontalSpacing="md">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Health</Table.Th>
                <Table.Th>Models</Table.Th>
                <Table.Th>Created</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>{rows}</Table.Tbody>
          </Table>
        </Table.ScrollContainer>

        {providers.length === 0 && (
          <Box p="xl" style={{ textAlign: 'center' }}>
            <Text c="dimmed">No providers configured. Add your first provider to get started.</Text>
          </Box>
        )}
      </Box>
    </Paper>
  );
}
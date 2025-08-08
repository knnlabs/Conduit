'use client';

import {
  Group,
  Text,
  Badge,
  Menu,
  rem,
  Box,
  Paper,
  Stack,
  ActionIcon,
  Table,
  Tooltip,
} from '@mantine/core';
import {
  IconTestPipe,
  IconEdit,
  IconTrash,
  IconDotsVertical,
  IconKey,
  IconAlertCircle,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { formatters } from '@/lib/utils/formatters';
import type { ProviderCredentialDto } from '@knn_labs/conduit-admin-client';
import { useRouter } from 'next/navigation';
import { getProviderDisplayName } from '@/lib/utils/providerTypeUtils';

// Use SDK types directly with health extensions  
interface Provider extends ProviderCredentialDto {
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  models?: string[];
  keyCount?: number;
}

interface ProvidersTableProps {
  onEdit?: (provider: Provider) => void;
  onTest?: (providerId: number) => void;
  onDelete?: (providerId: number) => void;
  data?: Provider[];
  testingProviders?: Set<number>;
}

export function ProvidersTable({ onEdit, onTest, onDelete, data, testingProviders = new Set() }: ProvidersTableProps) {
  const providers = data ?? [];
  const router = useRouter();

  const handleDelete = (provider: Provider) => {
    void modals.openConfirmModal({
      title: 'Delete Provider',
      children: (
        <Text size="sm">
          Are you sure you want to delete {provider.providerName ?? (provider.providerType ? getProviderDisplayName(provider.providerType) : 'Unknown Provider')}? This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => provider.id && onDelete?.(provider.id),
    });
  };

  const rows = providers.map((provider) => (
    <Table.Tr key={`provider-${provider.id}`}>
      <Table.Td>
        <Stack gap={4}>
          <Text fw={500}>{provider.providerName ?? (provider.providerType ? getProviderDisplayName(provider.providerType) : 'Unknown Provider')}</Text>
          <Text size="xs" c="dimmed">ID: {provider.id}</Text>
        </Stack>
      </Table.Td>

      <Table.Td>
        <Badge
          color="blue"
          variant="light"
          size="sm"
        >
          {provider.providerType ? getProviderDisplayName(provider.providerType) : 'Unknown'}
        </Badge>
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
        <Group gap="xs">
          {provider.keyCount === 0 ? (
            <Tooltip label="No API keys configured">
              <Badge
                color="orange"
                variant="light"
                size="sm"
                leftSection={<IconAlertCircle size={14} />}
              >
                No Keys
              </Badge>
            </Tooltip>
          ) : (
            <Badge
              color="blue"
              variant="light"
              size="sm"
              leftSection={<IconKey size={14} />}
            >
              {provider.keyCount} {provider.keyCount === 1 ? 'Key' : 'Keys'}
            </Badge>
          )}
        </Group>
      </Table.Td>

      <Table.Td>
        <Group gap={4}>
          {provider.models?.slice(0, 2).map((model) => (
            <Badge key={`model-${model}`} size="xs" variant="light">
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
          {provider.createdAt ? formatters.date(provider.createdAt) : '-'}
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
                leftSection={<IconKey style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => router.push(`/llm-providers/${provider.id}/keys`)}
              >
                Manage Keys
              </Menu.Item>
              <Menu.Item
                leftSection={<IconTestPipe style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => provider.id && onTest?.(provider.id)}
                disabled={provider.id ? testingProviders.has(provider.id) : true}
              >
                {provider.id && testingProviders.has(provider.id) ? 'Testing...' : 'Test Connection'}
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
                <Table.Th>Type</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>API Keys</Table.Th>
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
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
  Paper,
  Tooltip,
  Stack,
} from '@mantine/core';
import {
  IconDots,
  IconEdit,
  IconTrash,
  IconTestPipe,
  IconRefresh,
  IconCircleCheck,
  IconCircleX,
  IconClock,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { formatters } from '@/lib/utils/formatters';
import { badgeHelpers } from '@/lib/utils/badge-helpers';

interface Provider {
  id: string;
  providerName: string;
  providerType?: string;
  isEnabled: boolean;
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  modelsAvailable?: number;
  createdAt: string;
  apiEndpoint?: string;
  description?: string;
  organizationId?: string;
}

interface ProvidersTableProps {
  onEdit?: (provider: Provider) => void;
  onTest?: (providerId: string) => void;
  onDelete?: (providerId: string) => void;
  data?: Provider[];
  testingProviders?: Set<string>;
}

export function ProvidersTable({ onEdit, onTest, onDelete, data, testingProviders }: ProvidersTableProps) {
  const providers = data || [];

  const handleDelete = (provider: Provider) => {
    modals.openConfirmModal({
      title: 'Delete Provider',
      children: (
        <Text size="sm">
          Are you sure you want to delete the provider &quot;{provider.providerName}&quot;? 
          This action cannot be undone and will affect all model mappings using this provider.
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

  const rows = providers.map((provider) => {
    const isTestingProvider = testingProviders?.has(provider.id);
    
    return (
      <Table.Tr key={provider.id}>
        <Table.Td>
          <Stack gap={4}>
            <Text fw={500}>{provider.providerName}</Text>
            {provider.description && (
              <Text size="xs" c="dimmed">
                {provider.description}
              </Text>
            )}
          </Stack>
        </Table.Td>

        <Table.Td>
          <Badge variant="light">
            {provider.providerType || 'Unknown'}
          </Badge>
        </Table.Td>

        <Table.Td>
          <Badge 
            color={badgeHelpers.getStatusColor(provider.isEnabled)} 
            variant={provider.isEnabled ? 'light' : 'filled'}
          >
            {badgeHelpers.formatStatus(provider.isEnabled)}
          </Badge>
        </Table.Td>

        <Table.Td>
          <Group gap="xs">
            <Badge
              leftSection={getHealthIcon(provider.healthStatus)}
              color={getHealthColor(provider.healthStatus)}
              variant="light"
            >
              {provider.healthStatus}
            </Badge>
            {provider.lastHealthCheck && (
              <Tooltip label={`Last checked: ${formatters.date(provider.lastHealthCheck)}`}>
                <ActionIcon
                  size="sm"
                  variant="subtle"
                  onClick={() => onTest?.(provider.id)}
                  loading={isTestingProvider}
                >
                  <IconRefresh size={14} />
                </ActionIcon>
              </Tooltip>
            )}
          </Group>
        </Table.Td>

        <Table.Td>
          <Text size="sm">
            {provider.modelsAvailable !== undefined ? provider.modelsAvailable : 'N/A'}
          </Text>
        </Table.Td>

        <Table.Td>
          <Text size="sm" c="dimmed">
            {formatters.date(provider.createdAt)}
          </Text>
        </Table.Td>

        <Table.Td>
          <Group gap={0} justify="flex-end">
            <Tooltip label="Test connection">
              <ActionIcon 
                variant="subtle" 
                color="gray"
                onClick={() => onTest?.(provider.id)}
                loading={isTestingProvider}
              >
                <IconTestPipe size={16} />
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
                  onClick={() => onEdit?.(provider)}
                >
                  Edit
                </Menu.Item>
                
                <Menu.Item
                  leftSection={<IconTestPipe style={{ width: rem(14), height: rem(14) }} />}
                  onClick={() => onTest?.(provider.id)}
                >
                  Test Connection
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
    );
  });

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
                <Table.Th>Health</Table.Th>
                <Table.Th>Models</Table.Th>
                <Table.Th>Created</Table.Th>
                <Table.Th></Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows}
            </Table.Tbody>
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
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
  LoadingOverlay,
  Alert,
  Indicator,
  Tooltip,
  Stack,
} from '@mantine/core';
import {
  IconDots,
  IconEdit,
  IconTrash,
  IconTestPipe,
  IconRefresh,
  IconAlertCircle,
  IconCircleCheck,
  IconCircleX,
  IconClock,
} from '@tabler/icons-react';
import { useProviders, useDeleteProvider } from '@/hooks/api/useAdminApi';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';
import { badgeHelpers } from '@/lib/utils/badge-helpers';

interface Provider {
  id: string;
  providerName: string;
  providerType?: string;
  isEnabled: boolean;
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  modelsAvailable: number;
  createdAt: string;
  apiEndpoint?: string;
  description?: string;
  organizationId?: string;
}

interface ProvidersTableProps {
  onEdit?: (provider: Provider) => void;
  onTest?: (provider: Provider) => void;
  data?: Provider[];
}

export function ProvidersTable({ onEdit, onTest, data }: ProvidersTableProps) {
  const { data: defaultProviders, isLoading, error, refetch } = useProviders();
  const deleteProvider = useDeleteProvider();
  
  // Use provided data or default to fetched data
  const providers = data || defaultProviders;

  const handleDelete = (provider: Provider) => {
    modals.openConfirmModal({
      title: 'Delete Provider',
      children: (
        <Text size="sm">
          Are you sure you want to delete the provider &quot;{provider.providerName}&quot;? 
          This action cannot be undone and will remove all associated model mappings.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: async () => {
        await deleteProvider.mutateAsync(provider.id);
      },
    });
  };

  const handleRefresh = () => {
    refetch();
    notifications.show({
      title: 'Refreshing',
      message: 'Provider list refreshed',
      color: 'blue',
    });
  };

  const getHealthStatusIcon = (status: string) => {
    switch (status) {
      case 'healthy':
        return <IconCircleCheck size={16} color="green" />;
      case 'unhealthy':
        return <IconCircleX size={16} color="red" />;
      default:
        return <IconClock size={16} color="gray" />;
    }
  };


  if (error) {
    return (
      <Alert 
        icon={<IconAlertCircle size={16} />} 
        title="Error loading providers"
        color="red"
      >
        {error.message || 'Failed to load providers. Please try again.'}
      </Alert>
    );
  }

  const rows = providers?.map((provider: Provider) => (
    <Table.Tr key={provider.id}>
      <Table.Td>
        <Stack gap={4}>
          <Group gap="xs">
            <Text fw={500}>{provider.providerName}</Text>
            <Indicator 
              color={badgeHelpers.getHealthColor(provider.healthStatus)} 
              size={8}
            />
          </Group>
          {provider.description && (
            <Text size="xs" c="dimmed" lineClamp={1}>
              {provider.description}
            </Text>
          )}
        </Stack>
      </Table.Td>

      <Table.Td>
        <Badge 
          color={badgeHelpers.getStatusColor(provider.isEnabled)} 
          variant={provider.isEnabled ? 'light' : 'filled'}
        >
          {badgeHelpers.formatStatus(provider.isEnabled, { activeText: 'Enabled', inactiveText: 'Disabled' })}
        </Badge>
      </Table.Td>

      <Table.Td>
        <Group gap="xs">
          {getHealthStatusIcon(provider.healthStatus)}
          <Text size="sm">{badgeHelpers.getStatusConfig(provider.healthStatus, 'health').label}</Text>
        </Group>
      </Table.Td>

      <Table.Td>
        <Text size="sm">{provider.modelsAvailable}</Text>
      </Table.Td>

      <Table.Td>
        <Text size="sm" c="dimmed">
          {formatters.date(provider.lastHealthCheck)}
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
              color="blue"
              onClick={() => onTest?.(provider)}
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
                onClick={() => onTest?.(provider)}
              >
                Test connection
              </Menu.Item>
              
              <Menu.Item
                leftSection={<IconRefresh style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => handleRefresh()}
              >
                Refresh models
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
        <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
        
        <Table.ScrollContainer minWidth={800}>
          <Table verticalSpacing="sm" horizontalSpacing="md">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Health</Table.Th>
                <Table.Th>Models</Table.Th>
                <Table.Th>Last Check</Table.Th>
                <Table.Th>Created</Table.Th>
                <Table.Th></Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>

        {providers && providers.length === 0 && (
          <Box p="xl" style={{ textAlign: 'center' }}>
            <Text c="dimmed">No providers configured. Add your first provider to get started.</Text>
          </Box>
        )}
      </Box>
    </Paper>
  );
}
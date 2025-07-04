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
  Tooltip,
  Stack,
  Code,
} from '@mantine/core';
import {
  IconDots,
  IconEdit,
  IconTrash,
  IconTestPipe,
  IconAlertCircle,
  IconArrowRight,
  IconRefresh,
} from '@tabler/icons-react';
import { useModelMappings, useDeleteModelMapping } from '@/hooks/api/useAdminApi';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';

interface ModelMapping {
  id: string;
  internalModelName: string;
  providerModelName: string;
  providerName: string;
  isEnabled: boolean;
  capabilities: string[];
  priority: number;
  createdAt: string;
  lastUsed?: string;
  requestCount: number;
}

interface ModelMappingsTableProps {
  onEdit?: (mapping: ModelMapping) => void;
  onTest?: (mapping: ModelMapping) => void;
  data?: ModelMapping[];
  showProvider?: boolean;
}

export function ModelMappingsTable({ onEdit, onTest, data, showProvider: _showProvider = true }: ModelMappingsTableProps) {
  const { data: defaultMappings, isLoading, error, refetch } = useModelMappings();
  const deleteModelMapping = useDeleteModelMapping();
  
  // Use provided data or default to fetched data
  const modelMappings = data || defaultMappings;

  const handleDelete = (mapping: ModelMapping) => {
    modals.openConfirmModal({
      title: 'Delete Model Mapping',
      children: (
        <Text size="sm">
          Are you sure you want to delete the mapping for &quot;{mapping.internalModelName}&quot;? 
          This will prevent routing requests to this model configuration.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: async () => {
        await deleteModelMapping.mutateAsync(mapping.id);
      },
    });
  };

  const handleTestLocal = (mapping: ModelMapping) => {
    if (onTest) {
      onTest(mapping);
    }
  };

  const handleRefresh = () => {
    refetch();
    notifications.show({
      title: 'Refreshing',
      message: 'Model mappings refreshed',
      color: 'blue',
    });
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

  const getCapabilityColor = (capability: string) => {
    const colors: Record<string, string> = {
      'chat': 'blue',
      'completion': 'green',
      'embedding': 'purple',
      'image': 'pink',
      'video': 'red',
      'audio': 'teal',
      'function-calling': 'orange',
      'streaming': 'cyan',
    };
    return colors[capability.toLowerCase()] || 'gray';
  };

  if (error) {
    return (
      <Alert 
        icon={<IconAlertCircle size={16} />} 
        title="Error loading model mappings"
        color="red"
      >
        {error.message || 'Failed to load model mappings. Please try again.'}
      </Alert>
    );
  }

  const rows = modelMappings?.map((mapping: ModelMapping) => (
    <Table.Tr key={mapping.id}>
      <Table.Td>
        <Stack gap={4}>
          <Group gap="xs">
            <Code>{mapping.internalModelName}</Code>
            <IconArrowRight size={12} style={{ color: 'var(--mantine-color-dimmed)' }} />
            <Text size="sm" c="dimmed">{mapping.providerModelName}</Text>
          </Group>
          <Text size="xs" c="dimmed">
            via {mapping.providerName}
          </Text>
        </Stack>
      </Table.Td>

      <Table.Td>
        <Badge 
          color={mapping.isEnabled ? 'green' : 'red'} 
          variant={mapping.isEnabled ? 'light' : 'filled'}
        >
          {mapping.isEnabled ? 'Active' : 'Disabled'}
        </Badge>
      </Table.Td>

      <Table.Td>
        <Text size="sm" fw={500}>
          {mapping.priority}
        </Text>
      </Table.Td>

      <Table.Td>
        <Group gap={4}>
          {mapping.capabilities.slice(0, 3).map((capability) => (
            <Badge 
              key={capability}
              size="xs" 
              variant="dot" 
              color={getCapabilityColor(capability)}
            >
              {capability}
            </Badge>
          ))}
          {mapping.capabilities.length > 3 && (
            <Tooltip label={mapping.capabilities.slice(3).join(', ')}>
              <Badge size="xs" variant="light" color="gray">
                +{mapping.capabilities.length - 3}
              </Badge>
            </Tooltip>
          )}
        </Group>
      </Table.Td>

      <Table.Td>
        <Text size="sm">{mapping.requestCount.toLocaleString()}</Text>
      </Table.Td>

      <Table.Td>
        <Text size="sm" c="dimmed">
          {mapping.lastUsed ? formatDate(mapping.lastUsed) : 'Never'}
        </Text>
      </Table.Td>

      <Table.Td>
        <Group gap={0} justify="flex-end">
          <Tooltip label="Test model">
            <ActionIcon 
              variant="subtle" 
              color="blue"
              onClick={() => handleTestLocal(mapping)}
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
                onClick={() => onEdit?.(mapping)}
              >
                Edit
              </Menu.Item>
              
              <Menu.Item
                leftSection={<IconTestPipe style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => handleTestLocal(mapping)}
              >
                Test model
              </Menu.Item>
              
              <Menu.Item
                leftSection={<IconRefresh style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => handleRefresh()}
              >
                Refresh capabilities
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
    <Paper withBorder radius="md">
      <Box pos="relative">
        <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
        
        <Table.ScrollContainer minWidth={900}>
          <Table verticalSpacing="sm" horizontalSpacing="md">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Model Mapping</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Priority</Table.Th>
                <Table.Th>Capabilities</Table.Th>
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

        {modelMappings && modelMappings.length === 0 && (
          <Box p="xl" style={{ textAlign: 'center' }}>
            <Text c="dimmed">No model mappings configured. Add your first mapping to start routing requests.</Text>
          </Box>
        )}
      </Box>
    </Paper>
  );
}
'use client';

import {
  Group,
  Text,
  ActionIcon,
  Badge,
<<<<<<< HEAD
  Menu,
  rem,
  Box,
=======
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
  Tooltip,
} from '@mantine/core';
import {
  IconTestPipe,
  IconArrowRight,
} from '@tabler/icons-react';
<<<<<<< HEAD
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
=======
import { useModelMappings, useDeleteModelMapping } from '@/hooks/useConduitAdmin';
import { BaseTable, type ColumnDef, type ActionDef, type DeleteConfirmation } from '@/components/common/BaseTable';
import { useTableData, tableFormatters } from '@/hooks/useTableData';
import { badgeHelpers } from '@/lib/utils/badge-helpers';

import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6

interface ModelMappingsTableProps {
  data: any[];
  onEdit?: (mapping: any) => void;
  onTest?: (mappingId: string) => void;
  onDelete?: (mappingId: string) => void;
  testingMappings?: Set<string>;
}

<<<<<<< HEAD
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
=======
export function ModelMappingsTable({ onEdit, onTest, data, showProvider: _showProvider = true }: ModelMappingsTableProps) {
  const queryResult = useModelMappings();
  const deleteModelMapping = useDeleteModelMapping();
  
  const { handleRefresh, handleDelete } = useTableData({
    queryResult,
    deleteMutation: deleteModelMapping,
    refreshMessage: 'Model mappings refreshed',
    deleteSuccessMessage: 'Model mapping deleted successfully',
    deleteErrorMessage: 'Failed to delete model mapping',
  });

  // Use provided data or default to fetched data
  const modelMappings = data || queryResult.data || [];

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

  const renderCapabilities = (mapping: ModelProviderMappingDto) => {
    // Build capabilities array from boolean flags
    const capabilities = [];
    if (mapping.supportsVision) capabilities.push('vision');
    if (mapping.supportsImageGeneration) capabilities.push('image_generation');
    if (mapping.supportsAudioTranscription) capabilities.push('audio_transcription');
    if (mapping.supportsTextToSpeech) capabilities.push('text_to_speech');
    if (mapping.supportsRealtimeAudio) capabilities.push('realtime_audio');
    if (mapping.supportsFunctionCalling) capabilities.push('function_calling');
    if (mapping.supportsStreaming) capabilities.push('streaming');
    
    const badges = [];
    for (let i = 0; i < Math.min(3, capabilities.length); i++) {
      const capability = capabilities[i];
      badges.push(
        <Badge 
          key={capability}
          size="xs" 
          variant="dot" 
          color={getCapabilityColor(capability)}
        >
          {capability}
        </Badge>
      );
    }
    
    if (capabilities.length > 3) {
      badges.push(
        <Tooltip key="more" label={capabilities.slice(3).join(', ')}>
          <Badge size="xs" variant="light" color="gray">
            +{capabilities.length - 3}
          </Badge>
        </Tooltip>
      );
    }
    
    return <Group gap={4}>{badges}</Group>;
  };

  const columns: ColumnDef<ModelProviderMappingDto>[] = [
    {
      key: 'mapping',
      label: 'Model Mapping',
      render: (mapping) => (
        <Stack gap={4}>
          <Group gap="xs">
            <Code>{mapping.modelId}</Code>
            <IconArrowRight size={12} style={{ color: 'var(--mantine-color-dimmed)' }} />
            <Text size="sm" c="dimmed">{mapping.providerModelId}</Text>
          </Group>
          <Text size="xs" c="dimmed">
            via {mapping.providerId}
          </Text>
        </Stack>
      ),
    },
    {
      key: 'status',
      label: 'Status',
      render: (mapping) => (
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
        <Badge 
          color={mapping.isEnabled ? 'green' : 'gray'} 
          variant="light"
          size="sm"
        >
          {mapping.isEnabled ? 'Enabled' : 'Disabled'}
        </Badge>
<<<<<<< HEAD
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
=======
      ),
    },
    {
      key: 'priority',
      label: 'Priority',
      render: (mapping) => (
        <Text size="sm" fw={500}>
          {mapping.priority}
        </Text>
      ),
    },
    {
      key: 'capabilities',
      label: 'Capabilities',
      render: renderCapabilities,
    },
    {
      key: 'requests',
      label: 'Requests',
      render: () => <Text size="sm">-</Text>,
    },
    {
      key: 'lastUsed',
      label: 'Last Used',
      render: (mapping) => (
        <Text size="sm" c="dimmed">
          {tableFormatters.date(mapping.updatedAt)}
        </Text>
      ),
    },
  ];

  const customActions: ActionDef<ModelProviderMappingDto>[] = [
    {
      label: 'Test model',
      icon: IconTestPipe,
      onClick: (mapping) => onTest?.(mapping),
      color: 'blue',
    },
    {
      label: 'Refresh capabilities',
      icon: IconRefresh,
      onClick: () => handleRefresh(),
      color: 'blue',
    },
  ];

  const deleteConfirmation: DeleteConfirmation<ModelProviderMappingDto> = {
    title: 'Delete Model Mapping',
    message: (mapping) => 
      `Are you sure you want to delete the mapping for "${mapping.modelId}"? This will prevent routing requests to this model configuration.`,
  };

  return (
    <BaseTable
      data={modelMappings}
      isLoading={queryResult.isLoading}
      error={queryResult.error}
      columns={columns}
      onEdit={onEdit}
      onDelete={(mapping) => handleDelete(mapping.id.toString())}
      onRefresh={handleRefresh}
      customActions={customActions}
      deleteConfirmation={deleteConfirmation}
      emptyMessage="No model mappings configured. Add your first mapping to start routing requests."
      minWidth={900}
    />
>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6
  );
}
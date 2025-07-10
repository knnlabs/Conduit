'use client';

import {
  Group,
  Text,
  ActionIcon,
  Badge,
  Tooltip,
  Stack,
  Code,
} from '@mantine/core';
import {
  IconTestPipe,
  IconArrowRight,
  IconRefresh,
} from '@tabler/icons-react';
import { useModelMappings, useDeleteModelMapping } from '@/hooks/useConduitAdmin';
import { BaseTable, type ColumnDef, type ActionDef, type DeleteConfirmation } from '@/components/common/BaseTable';
import { useTableData, tableFormatters } from '@/hooks/useTableData';
import { badgeHelpers } from '@/lib/utils/ui-helpers';

import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

interface ModelMappingsTableProps {
  onEdit?: (mapping: ModelProviderMappingDto) => void;
  onTest?: (mapping: ModelProviderMappingDto) => void;
  data?: ModelProviderMappingDto[];
  showProvider?: boolean;
}

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
        <Badge 
          color={badgeHelpers.getStatusColor(mapping.isEnabled)} 
          variant={mapping.isEnabled ? 'light' : 'filled'}
        >
          {badgeHelpers.formatStatus(mapping.isEnabled)}
        </Badge>
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
  );
}
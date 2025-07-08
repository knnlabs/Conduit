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
import { formatters } from '@/lib/utils/formatters';
import { badgeHelpers } from '@/lib/utils/badge-helpers';

import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

interface ModelMappingsTableProps {
  onEdit?: (mapping: ModelProviderMappingDto) => void;
  onTest?: (mapping: ModelProviderMappingDto) => void;
  data?: ModelProviderMappingDto[];
  showProvider?: boolean;
}

export function ModelMappingsTable({ onEdit, onTest, data, showProvider: _showProvider = true }: ModelMappingsTableProps) {
  const { data: defaultMappings, isLoading, error, refetch } = useModelMappings();
  const deleteModelMapping = useDeleteModelMapping();
  
  // Use provided data or default to fetched data
  // Ensure modelMappings is always an array
  const modelMappings = Array.isArray(data) ? data : Array.isArray(defaultMappings) ? defaultMappings : [];

  const handleDelete = (mapping: ModelProviderMappingDto) => {
    modals.openConfirmModal({
      title: 'Delete Model Mapping',
      children: (
        <Text size="sm">
          Are you sure you want to delete the mapping for &quot;{mapping.modelId}&quot;? 
          This will prevent routing requests to this model configuration.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: async () => {
        await deleteModelMapping.mutateAsync(mapping.id.toString());
      },
    });
  };

  const handleTestLocal = (mapping: ModelProviderMappingDto) => {
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

  const rows = modelMappings.map((mapping: ModelProviderMappingDto) => (
    <Table.Tr key={mapping.id}>
      <Table.Td>
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
      </Table.Td>

      <Table.Td>
        <Badge 
          color={badgeHelpers.getStatusColor(mapping.isEnabled)} 
          variant={mapping.isEnabled ? 'light' : 'filled'}
        >
          {badgeHelpers.formatStatus(mapping.isEnabled)}
        </Badge>
      </Table.Td>

      <Table.Td>
        <Text size="sm" fw={500}>
          {mapping.priority}
        </Text>
      </Table.Td>

      <Table.Td>
        <Group gap={4}>
          {(() => {
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
            
            return badges;
          })()}
        </Group>
      </Table.Td>

      <Table.Td>
        <Text size="sm">-</Text>
      </Table.Td>

      <Table.Td>
        <Text size="sm" c="dimmed">
          {formatters.date(mapping.updatedAt)}
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

        {modelMappings.length === 0 && (
          <Box p="xl" style={{ textAlign: 'center' }}>
            <Text c="dimmed">No model mappings configured. Add your first mapping to start routing requests.</Text>
          </Box>
        )}
      </Box>
    </Paper>
  );
}
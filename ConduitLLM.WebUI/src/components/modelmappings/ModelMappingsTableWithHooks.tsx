import { useEffect } from 'react';
import {
  Table,
  Group,
  Text,
  ActionIcon,
  Badge,
  Menu,
  rem,
  Box,
  LoadingOverlay,
  Alert,
} from '@mantine/core';
import {
  IconEdit,
  IconTrash,
  IconDotsVertical,
  IconArrowRight,
  IconAlertCircle,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { useRouter } from 'next/navigation';
import { 
  useModelMappings, 
  useDeleteModelMapping
} from '@/hooks/useModelMappingsApi';
import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

// Extend the DTO type to ensure provider property is available
interface ExtendedModelProviderMappingDto extends ModelProviderMappingDto {
  provider?: {
    id: number;
    providerType: number;
    displayName: string;
    isEnabled: boolean;
  };
}

interface ModelMappingsTableProps {
  onRefresh?: () => void;
}

export function ModelMappingsTable({ onRefresh }: ModelMappingsTableProps) {
  const { mappings, isLoading, error, refetch } = useModelMappings();
  const deleteMapping = useDeleteModelMapping();
  const router = useRouter();

  // Refresh data when onRefresh changes
  useEffect(() => {
    if (onRefresh) {
      void refetch();
    }
  }, [onRefresh, refetch]);

  const handleEdit = (mapping: ExtendedModelProviderMappingDto) => {
    router.push(`/model-mappings/edit/${mapping.id}`);
  };


  const handleDelete = (mapping: ExtendedModelProviderMappingDto) => {
    modals.openConfirmModal({
      title: 'Delete Model Mapping',
      children: (
        <Text size="sm">
          Are you sure you want to delete the mapping for model &quot;{mapping.modelId}&quot;?
          This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => { void deleteMapping.mutateAsync(mapping.id); },
    });
  };

  const getCapabilityBadges = (mapping: ExtendedModelProviderMappingDto) => {
    const capabilities = [];
    if (mapping.supportsChat) capabilities.push({ label: '💬 Chat', color: 'blue' });
    if (mapping.supportsVision) capabilities.push({ label: '👁️ Vision', color: 'cyan' });
    if (mapping.supportsImageGeneration) capabilities.push({ label: '🎨 Images', color: 'pink' });
    if (mapping.supportsAudioTranscription) capabilities.push({ label: '🎤 Audio', color: 'teal' });
    if (mapping.supportsTextToSpeech) capabilities.push({ label: '🔊 TTS', color: 'violet' });
    if (mapping.supportsRealtimeAudio) capabilities.push({ label: '📡 Realtime', color: 'orange' });
    
    if (mapping.supportsVideoGeneration) capabilities.push({ label: '🎬 Video', color: 'grape' });
    if (mapping.supportsEmbeddings) capabilities.push({ label: '🔢 Embeddings', color: 'indigo' });
    if (mapping.supportsFunctionCalling) capabilities.push({ label: '🔧 Functions', color: 'green' });
    if (mapping.supportsStreaming) capabilities.push({ label: '⚡ Streaming', color: 'gray' });
    
    // Check capabilities string for additional features (if it exists in response)
    if ('capabilities' in mapping && mapping.capabilities) {
      const caps = mapping.capabilities;
      if (caps.includes('function-calling') && !mapping.supportsFunctionCalling) {
        capabilities.push({ label: 'Functions', color: 'green' });
      }
      if (caps.includes('streaming') && !mapping.supportsStreaming) {
        capabilities.push({ label: 'Streaming', color: 'cyan' });
      }
      if (caps.includes('embeddings') && !mapping.supportsEmbeddings) {
        capabilities.push({ label: 'Embeddings', color: 'indigo' });
      }
    }
    
    return capabilities.slice(0, 5).map((cap) => (
      <Badge key={`${cap.label}-${cap.color}`} size="xs" variant="dot" color={cap.color}>
        {cap.label}
      </Badge>
    ));
  };

  if (error) {
    return (
      <Alert icon={<IconAlertCircle size={16} />} title="Error" color="red">
        Failed to load model mappings: {error instanceof Error ? error.message : 'Unknown error'}
      </Alert>
    );
  }

  if (mappings.length === 0 && !isLoading) {
    return (
      <Box p="md" pos="relative">
        <Text c="dimmed" ta="center">
          No model mappings found. Create your first mapping to get started.
        </Text>
      </Box>
    );
  }

  const rows = (mappings as ExtendedModelProviderMappingDto[]).map((mapping) => (
    <Table.Tr key={mapping.id}>
      <Table.Td>
        <Group gap="xs">
          <Text size="sm" fw={500}>{mapping.modelId}</Text>
          <IconArrowRight size={14} style={{ color: 'var(--mantine-color-dimmed)' }} />
          <Text size="sm" c="dimmed">{mapping.providerModelId}</Text>
        </Group>
      </Table.Td>
      
      <Table.Td>
        <Text size="sm">
          {mapping.provider?.displayName ?? mapping.providerId}
        </Text>
      </Table.Td>

      <Table.Td>
        <Group gap={4}>
          {getCapabilityBadges(mapping)}
          {/* Count total capabilities */}
          {(() => {
            const booleanCaps = [
              mapping.supportsVision,
              mapping.supportsImageGeneration,
              mapping.supportsAudioTranscription,
              mapping.supportsTextToSpeech,
              mapping.supportsRealtimeAudio,
              mapping.supportsFunctionCalling,
              mapping.supportsStreaming,
              'supportsVideoGeneration' in mapping && mapping.supportsVideoGeneration
            ].filter(Boolean).length;
            
            let stringCaps = 0;
            if ('capabilities' in mapping && mapping.capabilities) {
              const caps = mapping.capabilities;
              // Count unique capabilities in string that aren't already counted
              if (caps.includes('embeddings')) stringCaps++;
              if (caps.includes('function-calling') && !mapping.supportsFunctionCalling) stringCaps++;
              if (caps.includes('streaming') && !mapping.supportsStreaming) stringCaps++;
            }
            
            const totalCaps = booleanCaps + stringCaps;
            
            return totalCaps > 3 ? (
              <Badge size="xs" variant="light" color="gray">
                +{totalCaps - 3}
              </Badge>
            ) : null;
          })()}
        </Group>
      </Table.Td>

      <Table.Td>
        <Text size="sm">{mapping.priority}</Text>
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
                onClick={() => handleEdit(mapping)}
              >
                Edit
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
    <Box pos="relative">
      <LoadingOverlay visible={isLoading} />
      <Table.ScrollContainer minWidth={800}>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Model Mapping</Table.Th>
              <Table.Th>Provider</Table.Th>
              <Table.Th>Capabilities</Table.Th>
              <Table.Th>Priority</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>{rows}</Table.Tbody>
        </Table>
      </Table.ScrollContainer>
    </Box>
  );
}
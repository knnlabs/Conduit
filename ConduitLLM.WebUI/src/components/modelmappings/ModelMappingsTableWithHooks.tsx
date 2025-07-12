import { useState, useEffect, useMemo } from 'react';
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
  LoadingOverlay,
  Alert,
} from '@mantine/core';
import {
  IconEdit,
  IconTrash,
  IconDotsVertical,
  IconTestPipe,
  IconArrowRight,
  IconAlertCircle,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { 
  useModelMappings, 
  useDeleteModelMapping, 
  useTestModelMapping 
} from '@/hooks/useModelMappingsApi';
import { useProviders } from '@/hooks/useProviderApi';
import { EditModelMappingModal } from './EditModelMappingModalWithHooks';
import { toUIModelMapping } from '@/lib/types/mappers';
import type { ModelProviderMappingDto } from '@/types/api-types';

interface ModelMappingsTableProps {
  onRefresh?: () => void;
}

export function ModelMappingsTable({ onRefresh }: ModelMappingsTableProps) {
  const { mappings, isLoading, error, refetch } = useModelMappings();
  const { providers } = useProviders();
  const deleteMapping = useDeleteModelMapping();
  const testMapping = useTestModelMapping();
  const [editingMapping, setEditingMapping] = useState<ModelProviderMappingDto | null>(null);
  const [testingMappings, setTestingMappings] = useState<Set<number>>(new Set());
  
  // Create a provider ID to name lookup map
  const providerIdToName = useMemo(() => {
    const map = new Map<string, string>();
    providers?.forEach(provider => {
      map.set(provider.id.toString(), provider.name);
    });
    return map;
  }, [providers]);

  // Refresh data when onRefresh changes
  useEffect(() => {
    if (onRefresh) {
      refetch();
    }
  }, [onRefresh, refetch]);

  const handleEdit = (mapping: ModelProviderMappingDto) => {
    setEditingMapping(mapping);
  };

  const handleTest = async (mappingId: number) => {
    setTestingMappings(prev => new Set(prev).add(mappingId));
    try {
      await testMapping.mutateAsync(mappingId);
    } finally {
      setTestingMappings(prev => {
        const next = new Set(prev);
        next.delete(mappingId);
        return next;
      });
    }
  };

  const handleDelete = (mapping: ModelProviderMappingDto) => {
    modals.openConfirmModal({
      title: 'Delete Model Mapping',
      children: (
        <Text size="sm">
          Are you sure you want to delete the mapping for model "{mapping.modelId}"?
          This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: async () => {
        await deleteMapping.mutateAsync(mapping.id);
      },
    });
  };

  const getCapabilityBadges = (mapping: ModelProviderMappingDto) => {
    const capabilities = [];
    if (mapping.supportsVision) capabilities.push({ label: 'Vision', color: 'blue' });
    if (mapping.supportsImageGeneration) capabilities.push({ label: 'Images', color: 'pink' });
    if (mapping.supportsAudioTranscription) capabilities.push({ label: 'Audio', color: 'teal' });
    if (mapping.supportsTextToSpeech) capabilities.push({ label: 'TTS', color: 'violet' });
    if (mapping.supportsRealtimeAudio) capabilities.push({ label: 'Realtime', color: 'orange' });
    
    // These fields exist in type definitions but may not be in actual SDK response
    if ('supportsVideoGeneration' in mapping && mapping.supportsVideoGeneration) {
      capabilities.push({ label: 'Video', color: 'grape' });
    }
    if (mapping.supportsFunctionCalling) capabilities.push({ label: 'Functions', color: 'green' });
    if (mapping.supportsStreaming) capabilities.push({ label: 'Streaming', color: 'cyan' });
    
    // Check capabilities string for additional features (if it exists in response)
    if ('capabilities' in mapping && mapping.capabilities) {
      const caps = mapping.capabilities as string;
      if (caps.includes('function-calling') && !mapping.supportsFunctionCalling) {
        capabilities.push({ label: 'Functions', color: 'green' });
      }
      if (caps.includes('streaming') && !mapping.supportsStreaming) {
        capabilities.push({ label: 'Streaming', color: 'cyan' });
      }
      if (caps.includes('embeddings')) {
        capabilities.push({ label: 'Embeddings', color: 'indigo' });
      }
    }
    
    return capabilities.slice(0, 3).map((cap, index) => (
      <Badge key={index} size="xs" variant="dot" color={cap.color}>
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

  const rows = mappings.map((mapping: ModelProviderMappingDto) => (
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
          {providerIdToName.get(mapping.providerId) || mapping.providerId}
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
              const caps = mapping.capabilities as string;
              // Count unique capabilities in string that aren't already counted
              const capsInString = caps.split(',').filter(c => c.trim());
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
              <Menu.Item
                leftSection={<IconTestPipe style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => handleTest(mapping.id)}
                disabled={testingMappings.has(mapping.id) || !mapping.isEnabled}
              >
                {testingMappings.has(mapping.id) ? 'Testing...' : 'Test'}
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
    <>
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

      {editingMapping && (
        <EditModelMappingModal
          mapping={toUIModelMapping(editingMapping)}
          isOpen={!!editingMapping}
          onClose={() => setEditingMapping(null)}
          onSave={async () => {
            setEditingMapping(null);
            await refetch();
          }}
        />
      )}
    </>
  );
}
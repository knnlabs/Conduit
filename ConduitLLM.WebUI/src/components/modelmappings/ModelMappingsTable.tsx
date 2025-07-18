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
} from '@mantine/core';
import {
  IconEdit,
  IconTrash,
  IconDotsVertical,
  IconArrowRight,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';

interface ModelMapping {
  id: string;
  modelName: string;
  providerModelId: string;
  providerName: string;
  priority: number;
  isEnabled: boolean;
  supportsVision?: boolean;
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
  supportsImageGeneration?: boolean;
  supportsAudioTranscription?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRealtimeAudio?: boolean;
  capabilities?: string;
}

interface ModelMappingsTableProps {
  data: ModelMapping[];
  onEdit?: (mapping: ModelMapping) => void;
  onDelete?: (mappingId: string) => void;
}

export function ModelMappingsTable({ 
  data,
  onEdit,
  onDelete,
}: ModelMappingsTableProps) {
  const handleDelete = (mapping: ModelMapping) => {
    modals.openConfirmModal({
      title: 'Delete Model Mapping',
      children: (
        <Text size="sm">
          Are you sure you want to delete the mapping for model &quot;{mapping.modelName}&quot;?
          This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => onDelete?.(mapping.id),
    });
  };

  const getCapabilityBadges = (mapping: ModelMapping) => {
    const capabilities = [];
    if (mapping.supportsVision) capabilities.push({ label: 'Vision', color: 'blue' });
    if (mapping.supportsFunctionCalling) capabilities.push({ label: 'Functions', color: 'green' });
    if (mapping.supportsStreaming) capabilities.push({ label: 'Streaming', color: 'cyan' });
    if (mapping.supportsImageGeneration) capabilities.push({ label: 'Images', color: 'pink' });
    if (mapping.supportsAudioTranscription) capabilities.push({ label: 'Audio', color: 'teal' });
    if (mapping.supportsTextToSpeech) capabilities.push({ label: 'TTS', color: 'violet' });
    if (mapping.supportsRealtimeAudio) capabilities.push({ label: 'Realtime', color: 'orange' });
    
    return capabilities.slice(0, 3).map((cap) => (
      <Badge key={`${cap.label}-${cap.color}`} size="xs" variant="dot" color={cap.color}>
        {cap.label}
      </Badge>
    ));
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
        <Text size="sm">{mapping.providerName ?? 'Unknown'}</Text>
      </Table.Td>

      <Table.Td>
        <Group gap={4}>
          {getCapabilityBadges(mapping)}
          {mapping.capabilities && mapping.capabilities.split(',').length > 3 && (
            <Tooltip label={mapping.capabilities}>
              <Badge size="xs" variant="light" color="gray">
                +{mapping.capabilities.split(',').length - 3}
              </Badge>
            </Tooltip>
          )}
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
                onClick={() => onEdit?.(mapping)}
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
  );
}
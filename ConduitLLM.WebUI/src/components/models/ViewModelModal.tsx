'use client';

import { Modal, Stack, Group, Text, Badge, Title, Divider } from '@mantine/core';
import type { ModelDto } from '@knn_labs/conduit-admin-client';

interface ViewModelModalProps {
  isOpen: boolean;
  model: ModelDto;
  onClose: () => void;
}

export function ViewModelModal({ isOpen, model, onClose }: ViewModelModalProps) {
  const getTypeBadgeColor = (type: number | undefined) => {
    switch (type) {
      case 0: return 'blue'; // Text/Chat
      case 1: return 'purple'; // Image
      case 2: return 'orange'; // Audio
      case 3: return 'pink'; // Video
      case 4: return 'green'; // Embedding
      default: return 'gray';
    }
  };
  
  const getTypeName = (type: number | undefined) => {
    switch (type) {
      case 0: return 'Text';
      case 1: return 'Image';
      case 2: return 'Audio';
      case 3: return 'Video';
      case 4: return 'Embedding';
      default: return 'Unknown';
    }
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={<Title order={3}>{model.name ?? 'Unnamed Model'}</Title>}
      size="lg"
    >
      <Stack>
        <Group justify="space-between">
          <Text fw={500}>Model Name:</Text>
          <Text>{model.name ?? '-'}</Text>
        </Group>

        <Group justify="space-between">
          <Text fw={500}>Type:</Text>
          <Badge color={getTypeBadgeColor(model.modelType)} variant="light">
            {getTypeName(model.modelType)}
          </Badge>
        </Group>

        <Group justify="space-between">
          <Text fw={500}>Status:</Text>
          <Badge color={model.isActive ? 'green' : 'gray'} variant="light">
            {model.isActive ? 'Active' : 'Inactive'}
          </Badge>
        </Group>

        <Divider />

        <Group justify="space-between">
          <Text fw={500}>Series ID:</Text>
          <Text>{model.modelSeriesId ?? '-'}</Text>
        </Group>

        {model.modelCapabilitiesId && (
          <Group justify="space-between">
            <Text fw={500}>Capabilities ID:</Text>
            <Text>{model.modelCapabilitiesId}</Text>
          </Group>
        )}

        <Divider />

        <Group justify="space-between">
          <Text fw={500} size="sm" c="dimmed">Created:</Text>
          <Text size="sm" c="dimmed">
            {model.createdAt ? new Date(model.createdAt).toLocaleString() : '-'}
          </Text>
        </Group>

        <Group justify="space-between">
          <Text fw={500} size="sm" c="dimmed">Updated:</Text>
          <Text size="sm" c="dimmed">
            {model.updatedAt ? new Date(model.updatedAt).toLocaleString() : '-'}
          </Text>
        </Group>
      </Stack>
    </Modal>
  );
}
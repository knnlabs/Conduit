'use client';

import { Group, Button, Text, Divider } from '@mantine/core';
import { IconTrash, IconDownload, IconX } from '@tabler/icons-react';

interface BulkActionsBarProps {
  selectedCount: number;
  onDeleteSelected: () => void;
  onDownloadSelected: () => void;
  onClearSelection: () => void;
}

export default function BulkActionsBar({
  selectedCount,
  onDeleteSelected,
  onDownloadSelected,
  onClearSelection,
}: BulkActionsBarProps) {
  if (selectedCount === 0) return null;

  return (
    <Group
      p="md"
      style={{
        backgroundColor: 'var(--mantine-color-blue-light)',
        borderRadius: 'var(--mantine-radius-md)',
      }}
    >
      <Text fw={500}>
        {selectedCount} item{selectedCount > 1 ? 's' : ''} selected
      </Text>
      
      <Divider orientation="vertical" />
      
      <Button
        size="sm"
        variant="light"
        color="red"
        leftSection={<IconTrash size={16} />}
        onClick={onDeleteSelected}
      >
        Delete Selected
      </Button>
      
      <Button
        size="sm"
        variant="light"
        leftSection={<IconDownload size={16} />}
        onClick={onDownloadSelected}
      >
        Download Selected
      </Button>
      
      <Button
        size="sm"
        variant="subtle"
        leftSection={<IconX size={16} />}
        onClick={onClearSelection}
        ml="auto"
      >
        Clear Selection
      </Button>
    </Group>
  );
}
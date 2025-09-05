import { Group, Button, Paper, Text, Transition } from '@mantine/core';
import { IconTrash, IconToggleLeft, IconToggleRight, IconX } from '@tabler/icons-react';
import { modals } from '@mantine/modals';

interface BulkActionsBarProps {
  selectedCount: number;
  onDelete: () => void;
  onEnable: () => void;
  onDisable: () => void;
  onClearSelection: () => void;
  isDeleting?: boolean;
  isEnabling?: boolean;
  isDisabling?: boolean;
}

export function BulkActionsBar({
  selectedCount,
  onDelete,
  onEnable,
  onDisable,
  onClearSelection,
  isDeleting = false,
  isEnabling = false,
  isDisabling = false,
}: BulkActionsBarProps) {
  const isVisible = selectedCount > 0;
  const isProcessing = isDeleting || isEnabling || isDisabling;

  const handleDelete = () => {
    modals.openConfirmModal({
      title: 'Delete Selected Mappings',
      children: (
        <Text size="sm">
          Are you sure you want to delete {selectedCount} selected mapping{selectedCount > 1 ? 's' : ''}?
          This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: onDelete,
    });
  };

  const handleEnable = () => {
    modals.openConfirmModal({
      title: 'Enable Selected Mappings',
      children: (
        <Text size="sm">
          Are you sure you want to enable {selectedCount} selected mapping{selectedCount > 1 ? 's' : ''}?
        </Text>
      ),
      labels: { confirm: 'Enable', cancel: 'Cancel' },
      confirmProps: { color: 'green' },
      onConfirm: onEnable,
    });
  };

  const handleDisable = () => {
    modals.openConfirmModal({
      title: 'Disable Selected Mappings',
      children: (
        <Text size="sm">
          Are you sure you want to disable {selectedCount} selected mapping{selectedCount > 1 ? 's' : ''}?
        </Text>
      ),
      labels: { confirm: 'Disable', cancel: 'Cancel' },
      confirmProps: { color: 'orange' },
      onConfirm: onDisable,
    });
  };

  return (
    <Transition
      mounted={isVisible}
      transition="slide-up"
      duration={200}
      timingFunction="ease"
    >
      {(styles) => (
        <Paper
          shadow="md"
          p="md"
          pos="fixed"
          bottom={20}
          left="50%"
          style={{
            ...styles,
            transform: 'translateX(-50%)',
            zIndex: 1000,
            minWidth: 400,
          }}
        >
          <Group justify="space-between">
            <Text size="sm" fw={500}>
              {selectedCount} item{selectedCount > 1 ? 's' : ''} selected
            </Text>
            <Group gap="xs">
              <Button
                size="sm"
                variant="light"
                color="green"
                leftSection={<IconToggleRight size={16} />}
                onClick={handleEnable}
                disabled={isProcessing}
                loading={isEnabling}
              >
                Enable
              </Button>
              <Button
                size="sm"
                variant="light"
                color="orange"
                leftSection={<IconToggleLeft size={16} />}
                onClick={handleDisable}
                disabled={isProcessing}
                loading={isDisabling}
              >
                Disable
              </Button>
              <Button
                size="sm"
                variant="light"
                color="red"
                leftSection={<IconTrash size={16} />}
                onClick={handleDelete}
                disabled={isProcessing}
                loading={isDeleting}
              >
                Delete
              </Button>
              <Button
                size="sm"
                variant="subtle"
                leftSection={<IconX size={16} />}
                onClick={onClearSelection}
                disabled={isProcessing}
              >
                Clear
              </Button>
            </Group>
          </Group>
        </Paper>
      )}
    </Transition>
  );
}
import { Modal, Text, Group, Button } from '@mantine/core';

interface ConfirmationModalProps {
  opened: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  confirmColor?: string;
  loading?: boolean;
}

export function ConfirmationModal({
  opened,
  onClose,
  onConfirm,
  title,
  message,
  confirmText = 'Confirm',
  cancelText = 'Cancel',
  confirmColor = 'blue',
  loading = false,
}: ConfirmationModalProps) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={title}
      centered
      size="md"
    >
      <Text mb="lg">{message}</Text>
      <Group justify="flex-end">
        <Button variant="light" onClick={onClose} disabled={loading}>
          {cancelText}
        </Button>
        <Button
          color={confirmColor}
          onClick={onConfirm}
          loading={loading}
        >
          {confirmText}
        </Button>
      </Group>
    </Modal>
  );
}
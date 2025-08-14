'use client';

import { useState } from 'react';
import { Modal, Stack, Text, Button, NumberInput, Alert, Group } from '@mantine/core';
import { IconTrash, IconAlertCircle } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';

interface CleanupModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export default function CleanupModal({ opened, onClose, onSuccess }: CleanupModalProps) {
  const [loading, setLoading] = useState(false);
  const [daysToKeep, setDaysToKeep] = useState(90);

  const handleCleanup = async (type: 'expired' | 'orphaned' | 'prune') => {
    setLoading(true);
    try {
      const response = await fetch('/api/media/cleanup', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ 
          type,
          ...(type === 'prune' && { daysToKeep })
        }),
      });

      if (!response.ok) {
        throw new Error('Cleanup failed');
      }

      const data = await response.json() as { message: string };
      
      notifications.show({
        title: 'Cleanup Successful',
        message: data.message,
        color: 'green',
      });

      onSuccess();
      onClose();
    } catch {
      notifications.show({
        title: 'Cleanup Failed',
        message: 'An error occurred during cleanup',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title="Media Cleanup Operations"
      size="md"
    >
      <Stack>
        <Alert icon={<IconAlertCircle size={16} />} color="orange">
          <Text size="sm">
            These operations will permanently delete media files. This action cannot be undone.
          </Text>
        </Alert>

        <Stack gap="md">
          <div>
            <Text fw={500} mb="xs">Cleanup Expired Media</Text>
            <Text size="sm" c="dimmed" mb="sm">
              Remove all media files that have passed their expiration date.
            </Text>
            <Button
              variant="light"
              color="orange"
              leftSection={<IconTrash size={16} />}
              onClick={() => void handleCleanup('expired')}
              loading={loading}
              fullWidth
            >
              Clean Expired Media
            </Button>
          </div>

          <div>
            <Text fw={500} mb="xs">Cleanup Orphaned Media</Text>
            <Text size="sm" c="dimmed" mb="sm">
              Remove media files that belong to deleted virtual keys.
            </Text>
            <Button
              variant="light"
              color="orange"
              leftSection={<IconTrash size={16} />}
              onClick={() => void handleCleanup('orphaned')}
              loading={loading}
              fullWidth
            >
              Clean Orphaned Media
            </Button>
          </div>

          <div>
            <Text fw={500} mb="xs">Prune Old Media</Text>
            <Text size="sm" c="dimmed" mb="sm">
              Remove media files older than a specified number of days.
            </Text>
            <NumberInput
              label="Days to keep"
              value={daysToKeep}
              onChange={(val) => setDaysToKeep(Number(val) || 90)}
              min={1}
              max={365}
              mb="sm"
            />
            <Button
              variant="light"
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={() => void handleCleanup('prune')}
              loading={loading}
              fullWidth
            >
              Prune Old Media
            </Button>
          </div>
        </Stack>

        <Group justify="flex-end" mt="md">
          <Button variant="default" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
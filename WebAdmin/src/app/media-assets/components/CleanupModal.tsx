'use client';

import { useState, useMemo } from 'react';
import { Modal, Stack, Text, Button, NumberInput, Alert, Group } from '@mantine/core';
import { IconTrash, IconAlertCircle } from '@tabler/icons-react';
import { useConfirmModal } from '@/hooks/useFormModal';
import { withAdminClient } from '@/lib/client/adminClient';

async function runCleanup(type: 'expired' | 'orphaned' | 'prune', daysToKeep?: number): Promise<void> {
  await withAdminClient(client =>
    client.media.cleanupMedia({
      type,
      ...(type === 'prune' && { daysToKeep })
    })
  );
}

interface CleanupModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export default function CleanupModal({ opened, onClose, onSuccess }: CleanupModalProps) {
  const [daysToKeep, setDaysToKeep] = useState(90);

  const { loading: expiredLoading, handleConfirm: handleExpired } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction: () => runCleanup('expired'),
    successMessage: 'Expired media cleaned up successfully',
  });

  const { loading: orphanedLoading, handleConfirm: handleOrphaned } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction: () => runCleanup('orphaned'),
    successMessage: 'Orphaned media cleaned up successfully',
  });

  const { loading: pruneLoading, handleConfirm: handlePrune } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction: () => runCleanup('prune', daysToKeep),
    successMessage: `Media older than ${daysToKeep} days pruned successfully`,
  });

  const loading = useMemo(
    () => expiredLoading || orphanedLoading || pruneLoading,
    [expiredLoading, orphanedLoading, pruneLoading]
  );

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
              onClick={() => void handleExpired()}
              loading={expiredLoading}
              disabled={loading && !expiredLoading}
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
              onClick={() => void handleOrphaned()}
              loading={orphanedLoading}
              disabled={loading && !orphanedLoading}
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
              onClick={() => void handlePrune()}
              loading={pruneLoading}
              disabled={loading && !pruneLoading}
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

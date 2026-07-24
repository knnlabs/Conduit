'use client';

import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Checkbox, Group, Modal, NumberInput, Stack, Text, TextInput } from '@mantine/core';
import { IconTrash, IconAlertCircle } from '@tabler/icons-react';
import { useConfirmModal } from '@/hooks/useFormModal';
import { withAdminClient } from '@/lib/client/adminClient';
import type { MediaCleanupPreview } from '@/lib/admin-api/models/media';

async function runCleanup(
  type: 'expired' | 'reconciliation' | 'prune',
  daysToKeep?: number,
  force = false
): Promise<void> {
  await withAdminClient(client =>
    client.media.cleanupMedia({
      type,
      force,
      ...(type === 'prune' && { daysToKeep }),
    })
  );
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const unit = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  return `${(bytes / 1024 ** unit).toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`;
}

interface CleanupModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export default function CleanupModal({ opened, onClose, onSuccess }: CleanupModalProps) {
  const [daysToKeep, setDaysToKeep] = useState(90);
  const [force, setForce] = useState(false);
  const [confirmation, setConfirmation] = useState('');
  const [preview, setPreview] = useState<MediaCleanupPreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [dryRunMode, setDryRunMode] = useState(true);

  useEffect(() => {
    if (!opened) return;

    let active = true;
    setPreviewLoading(true);
    setPreviewError(null);
    setConfirmation('');

    void Promise.all([
      withAdminClient(client => client.media.previewPruneMedia(daysToKeep)),
      withAdminClient(client => client.media.getCleanupServiceStatus()),
    ])
      .then(([nextPreview, status]) => {
        if (!active) return;
        setPreview(nextPreview);
        setDryRunMode(status.isDryRunMode);
      })
      .catch((error: unknown) => {
        if (!active) return;
        setPreview(null);
        setPreviewError(error instanceof Error ? error.message : 'Unable to load cleanup preview');
      })
      .finally(() => {
        if (active) setPreviewLoading(false);
      });

    return () => {
      active = false;
    };
  }, [daysToKeep, opened]);

  const { loading: expiredLoading, handleConfirm: handleExpired } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction: () => runCleanup('expired'),
    successMessage: 'Expired media cleaned up successfully',
  });

  const { loading: reconciliationLoading, handleConfirm: handleReconciliation } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction: () => runCleanup('reconciliation'),
    successMessage: 'Storage reconciliation completed successfully',
  });

  const { loading: pruneLoading, handleConfirm: handlePrune } = useConfirmModal({
    onClose,
    onSuccess,
    confirmAction: () => runCleanup('prune', daysToKeep, force),
    successMessage: dryRunMode && !force
      ? `Dry run completed for media older than ${daysToKeep} days`
      : `Media older than ${daysToKeep} days pruned successfully`,
  });

  const loading = useMemo(
    () => expiredLoading || reconciliationLoading || pruneLoading || previewLoading,
    [expiredLoading, reconciliationLoading, previewLoading, pruneLoading]
  );
  const destructivePrune = force || !dryRunMode;
  const confirmationMatches = !destructivePrune ||
    (preview !== null && confirmation === preview.confirmationPhrase);
  let previewMessage = 'No preview is available.';
  if (previewLoading) {
    previewMessage = 'Calculating preview…';
  } else if (preview) {
    const outcome = destructivePrune
      ? 'This run will permanently delete them.'
      : 'This run will be a dry run.';
    previewMessage = `${preview.fileCount.toLocaleString()} files (${formatBytes(preview.sizeBytes)}) match. ${outcome}`;
  }

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
            Cleanup respects the configured dry-run mode, monthly deletion budget, and global cleanup lock.
            A forced prune performs an audited permanent deletion and cannot be undone.
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
            <Text fw={500} mb="xs">Reconcile Storage</Text>
            <Text size="sm" c="dimmed" mb="sm">
              Find storage objects without tracking records. Objects newer than the configured
              safety window are always protected.
            </Text>
            <Button
              variant="light"
              color="orange"
              leftSection={<IconTrash size={16} />}
              onClick={() => void handleReconciliation()}
              loading={reconciliationLoading}
              disabled={loading && !reconciliationLoading}
              fullWidth
            >
              Reconcile Storage
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
            <Checkbox
              label="Force a real deletion even when dry-run mode is enabled"
              checked={force}
              onChange={(event) => {
                setForce(event.currentTarget.checked);
                setConfirmation('');
              }}
              mb="sm"
            />
            {previewError ? (
              <Alert color="red" mb="sm">
                {previewError}
              </Alert>
            ) : (
              <Alert color={destructivePrune ? 'red' : 'blue'} mb="sm">
                {previewMessage}
              </Alert>
            )}
            {destructivePrune && preview && (
              <TextInput
                label={`Type ${preview.confirmationPhrase} to confirm`}
                value={confirmation}
                onChange={(event) => setConfirmation(event.currentTarget.value)}
                mb="sm"
              />
            )}
            <Button
              variant="light"
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={() => void handlePrune()}
              loading={pruneLoading}
              disabled={(loading && !pruneLoading) || !preview || !confirmationMatches}
              fullWidth
            >
              {destructivePrune ? 'Permanently Prune Old Media' : 'Run Prune Dry Run'}
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

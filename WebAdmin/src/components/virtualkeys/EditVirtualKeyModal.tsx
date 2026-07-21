'use client';

import {
  Modal,
  TextInput,
  NumberInput,
  Switch,
  Stack,
  Text,
  Textarea,
  Alert,
  Button,
  Group,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertCircle } from '@tabler/icons-react';
import { validators } from '@/lib/utils/form-validators';
import { useState, useEffect, useRef } from 'react';

import type { VirtualKeyDto } from '@/lib/admin-api';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';

interface EditVirtualKeyModalProps {
  opened: boolean;
  onClose: () => void;
  virtualKey: VirtualKeyDto | null;
  onSuccess?: () => void;
}

interface EditVirtualKeyForm {
  keyName: string;
  description?: string;
  virtualKeyGroupId?: number;
  isEnabled: boolean;
  allowedModels: string[];
}

export function EditVirtualKeyModal({ opened, onClose, virtualKey, onSuccess }: EditVirtualKeyModalProps) {
  const [initialFormValues, setInitialFormValues] = useState<EditVirtualKeyForm>(() => ({
    keyName: '',
    description: '',
    virtualKeyGroupId: undefined,
    isEnabled: true,
    allowedModels: [],
  }));
  const lastVirtualKeyId = useRef<number | undefined>(undefined);

  const form = useForm<EditVirtualKeyForm>({
    initialValues: initialFormValues,
    validate: {
      keyName: (value) => {
        const requiredError = validators.required('Key name')(value);
        if (requiredError) return requiredError;

        const minLengthError = validators.minLength('Key name', 3)(value);
        if (minLengthError) return minLengthError;

        const maxLengthError = validators.maxLength('Key name', 100)(value);
        if (maxLengthError) return maxLengthError;

        return null;
      },
      virtualKeyGroupId: validators.positiveNumber('Virtual Key Group'),
    },
  });

  const { loading, handleSubmit, handleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction: (values) => {
      if (!virtualKey) return Promise.resolve();

      const payload = {
        keyName: values.keyName.trim(),
        virtualKeyGroupId: values.virtualKeyGroupId ?? undefined,
        isEnabled: values.isEnabled,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels.join(',') : undefined,
        metadata: values.description?.trim() ?? undefined,
      };

      return withAdminClient(client =>
        client.virtualKeys.update(virtualKey.id.toString(), payload)
      );
    },
    successMessage: 'Virtual key updated successfully',
  });

  // Reset tracking when modal closes
  useEffect(() => {
    if (!opened) {
      lastVirtualKeyId.current = undefined;
    }
  }, [opened]);

  // Update form when virtualKey changes
  useEffect(() => {
    if (!virtualKey) return;

    // Only update if this is a different virtualKey than last time
    if (lastVirtualKeyId.current === virtualKey.id) return;
    lastVirtualKeyId.current = virtualKey.id;

    // Parse allowedModels from string to array (it's stored as comma-separated in the DTO)
    const models = virtualKey.allowedModels
      ? virtualKey.allowedModels.split(',').map(m => m.trim()).filter(m => m)
      : ['*']; // Default to all models if none specified

    const newFormValues: EditVirtualKeyForm = {
      keyName: virtualKey.keyName,
      description: virtualKey.metadata ? JSON.stringify(virtualKey.metadata) : '',
      virtualKeyGroupId: virtualKey.virtualKeyGroupId ?? undefined,
      isEnabled: virtualKey.isEnabled,
      allowedModels: models,
    };

    setInitialFormValues(newFormValues);
    form.setValues(newFormValues);
    form.resetDirty();
  }, [virtualKey, form]);

  if (!virtualKey) {
    return null;
  }

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Edit Virtual Key"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
        <Alert icon={<IconAlertCircle size={16} />} color="blue">
          <Text size="sm" fw={500}>Key Prefix</Text>
          <Text size="xs" style={{ fontFamily: 'monospace' }}>
            {virtualKey.keyPrefix ?? 'N/A'}
          </Text>
        </Alert>

        <TextInput
          label="Key Name"
          placeholder="Enter a unique name for this key"
          required
          {...form.getInputProps('keyName')}
        />

        <Textarea
          label="Description"
          placeholder="Optional description for this key"
          rows={3}
          {...form.getInputProps('description')}
        />

        <Switch
          label="Enabled"
          description="Whether this key can be used for API requests"
          {...form.getInputProps('isEnabled', { type: 'checkbox' })}
        />

        <NumberInput
          label="Virtual Key Group"
          description="Group ID this key belongs to"
          placeholder="Group ID"
          min={1}
          step={1}
          {...form.getInputProps('virtualKeyGroupId')}
        />

        <Alert icon={<IconAlertCircle size={16} />} color="gray">
          <Text size="sm">
            Virtual Key Group ID: {virtualKey.virtualKeyGroupId}
          </Text>
        </Alert>

        <Group justify="flex-end" mt="md">
          <Button variant="subtle" onClick={handleClose}>
            Cancel
          </Button>
          <Button type="submit" loading={loading}>
            Save Changes
          </Button>
        </Group>
      </Stack>
      </form>
    </Modal>
  );
}

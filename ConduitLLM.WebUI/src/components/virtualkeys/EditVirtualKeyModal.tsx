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
import { notifications } from '@mantine/notifications';
import { useState, useEffect, useCallback } from 'react';

import type { VirtualKeyDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';

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
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [initialFormValues, setInitialFormValues] = useState<EditVirtualKeyForm>(() => ({
    keyName: '',
    description: '',
    virtualKeyGroupId: undefined,
    isEnabled: true,
    allowedModels: [],
  }));

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

  // Stable callback for form updates
  const updateForm = useCallback((newFormValues: EditVirtualKeyForm) => {
    setInitialFormValues(newFormValues);
    form.setValues(newFormValues);
    form.resetDirty();
  }, [form]);

  // Update form when virtualKey changes
  useEffect(() => {
    if (virtualKey) {
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
      
      updateForm(newFormValues);
    }
  }, [virtualKey, updateForm]);

  const handleSubmit = async (values: EditVirtualKeyForm) => {
    if (!virtualKey) return;

    setIsSubmitting(true);
    try {
      
      const payload = {
        keyName: values.keyName.trim(),
        virtualKeyGroupId: values.virtualKeyGroupId ?? undefined,
        isEnabled: values.isEnabled,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels.join(',') : undefined,
        // Note: description is stored in metadata for virtual keys
        metadata: values.description?.trim() ?? undefined,
      };

      await withAdminClient(client => 
        client.virtualKeys.update(virtualKey.id.toString(), payload)
      );
      
      // Response was successful

      notifications.show({
        title: 'Success',
        message: 'Virtual key updated successfully',
        color: 'green',
      });
      
      onClose();
      if (onSuccess) {
        onSuccess();
      }
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to update virtual key',
        color: 'red',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!virtualKey) {
    return null;
  }

  return (
    <Modal
      opened={opened}
      onClose={onClose}
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
          <Button variant="subtle" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Save Changes
          </Button>
        </Group>
      </Stack>
      </form>
    </Modal>
  );
}
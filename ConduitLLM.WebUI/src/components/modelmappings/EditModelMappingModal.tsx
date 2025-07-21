'use client';

import {
  Modal,
  TextInput,
  Switch,
  Stack,
  Group,
  NumberInput,
  Button,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useEffect, useState } from 'react';
import { notifications } from '@mantine/notifications';

interface ModelMapping {
  id: string;
  modelName: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
}

interface EditModelMappingModalProps {
  opened: boolean;
  onClose: () => void;
  mapping: ModelMapping | null;
  onSuccess?: () => void;
}

interface FormValues {
  modelName: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
}

export function EditModelMappingModal({
  opened,
  onClose,
  mapping,
  onSuccess,
}: EditModelMappingModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  const form = useForm<FormValues>({
    initialValues: {
      modelName: '',
      providerModelId: '',
      priority: 100,
      isEnabled: true,
    },
  });

  // Update form when mapping changes
  useEffect(() => {
    if (mapping) {
      form.setValues({
        modelName: mapping.modelName ?? '',
        providerModelId: mapping.providerModelId ?? '',
        priority: mapping.priority ?? 100,
        isEnabled: mapping.isEnabled ?? false,
      });
    }
  }, [mapping, form]);

  const handleSubmit = async (values: FormValues) => {
    if (!mapping) return;

    try {
      setIsSubmitting(true);
      const response = await fetch(`/api/model-mappings/${mapping.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(values),
      });

      if (!response.ok) {
        throw new Error('Failed to update model mapping');
      }

      notifications.show({
        title: 'Success',
        message: 'Model mapping updated successfully',
        color: 'green',
      });

      onClose();
      onSuccess?.();
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to update model mapping',
        color: 'red',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title="Edit Model Mapping"
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="Model Name"
            placeholder="e.g., gpt-4"
            disabled
            {...form.getInputProps('modelName')}
          />

          <TextInput
            label="Provider Model ID"
            placeholder="e.g., gpt-4-turbo"
            {...form.getInputProps('providerModelId')}
          />

          <NumberInput
            label="Priority"
            placeholder="100"
            min={0}
            max={1000}
            {...form.getInputProps('priority')}
          />

          <Switch
            label="Enable mapping"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

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
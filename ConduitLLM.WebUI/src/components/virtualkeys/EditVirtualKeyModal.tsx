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
import { useState, useEffect } from 'react';

interface VirtualKey {
  id: string;
  keyName: string;
  keyHash: string;
  currentSpend: number;
  maxBudget?: number;
  isEnabled: boolean;
  createdAt: string;
  lastUsed?: string;
  requestCount: number;
  description?: string;
  allowedModels?: string[];
}

interface EditVirtualKeyModalProps {
  opened: boolean;
  onClose: () => void;
  virtualKey: VirtualKey | null;
  onSuccess?: () => void;
}

interface EditVirtualKeyForm {
  keyName: string;
  description?: string;
  maxBudget?: number;
  isEnabled: boolean;
  allowedModels: string[];
}

export function EditVirtualKeyModal({ opened, onClose, virtualKey, onSuccess }: EditVirtualKeyModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  const form = useForm<EditVirtualKeyForm>({
    initialValues: {
      keyName: '',
      description: '',
      maxBudget: undefined,
      isEnabled: true,
      allowedModels: [],
    },
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
      maxBudget: validators.positiveNumber('Budget'),
    },
  });

  // Update form when virtualKey changes
  useEffect(() => {
    if (virtualKey) {
      form.setValues({
        keyName: virtualKey.keyName,
        description: virtualKey.description || '',
        maxBudget: virtualKey.maxBudget,
        isEnabled: virtualKey.isEnabled,
        allowedModels: virtualKey.allowedModels || [],
      });
    }
  }, [virtualKey]);

  const handleSubmit = async (values: EditVirtualKeyForm) => {
    if (!virtualKey) return;

    setIsSubmitting(true);
    try {
      const payload = {
        id: parseInt(virtualKey.id, 10),
        keyName: values.keyName.trim(),
        description: values.description?.trim() || undefined,
        maxBudget: values.maxBudget || undefined,
        isEnabled: values.isEnabled,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels.join(',') : undefined,
      };

      const response = await fetch(`/api/virtualkeys/${virtualKey.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        throw new Error('Failed to update virtual key');
      }

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
          <Text size="sm" fw={500}>Key Hash</Text>
          <Text size="xs" style={{ fontFamily: 'monospace' }}>
            {virtualKey.keyHash}
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
          label="Maximum Budget"
          description="Maximum amount this key can spend (in USD)"
          placeholder="No limit"
          min={0}
          step={10}
          decimalScale={2}
          prefix="$"
          {...form.getInputProps('maxBudget')}
        />

        <Alert icon={<IconAlertCircle size={16} />} color="gray">
          <Text size="sm">
            Current spend: ${virtualKey.currentSpend.toFixed(2)} | 
            Requests: {virtualKey.requestCount.toLocaleString()}
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
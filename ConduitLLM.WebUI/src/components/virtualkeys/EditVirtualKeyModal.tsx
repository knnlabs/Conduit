'use client';

import {
  Modal,
  TextInput,
  NumberInput,
  Switch,
  Button,
  Stack,
  Group,
  Text,
  Textarea,
  Alert,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useUpdateVirtualKey } from '@/hooks/api/useAdminApi';
import { IconAlertCircle } from '@tabler/icons-react';
import { useEffect } from 'react';

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
}

interface EditVirtualKeyForm {
  keyName: string;
  description?: string;
  maxBudget?: number;
  isEnabled: boolean;
  allowedModels: string[];
}

export function EditVirtualKeyModal({ opened, onClose, virtualKey }: EditVirtualKeyModalProps) {
  const updateVirtualKey = useUpdateVirtualKey();

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
        if (!value || value.trim().length === 0) {
          return 'Key name is required';
        }
        if (value.length < 3) {
          return 'Key name must be at least 3 characters';
        }
        if (value.length > 100) {
          return 'Key name must be less than 100 characters';
        }
        return null;
      },
      maxBudget: (value) => {
        if (value !== undefined && value <= 0) {
          return 'Budget must be greater than 0';
        }
        return null;
      },
    },
  });

  // Update form when virtual key changes
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

    try {
      const payload = {
        keyName: values.keyName.trim(),
        description: values.description?.trim() || undefined,
        maxBudget: values.maxBudget || undefined,
        isEnabled: values.isEnabled,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels : undefined,
      };

      await updateVirtualKey.mutateAsync({
        id: virtualKey.id,
        data: payload,
      });
      
      onClose();
    } catch (error: unknown) {
      // Error is handled by the mutation hook
      console.error('Update virtual key error:', error);
    }
  };

  const handleClose = () => {
    form.reset();
    onClose();
  };

  if (!virtualKey) {
    return null;
  }

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Edit Virtual Key"
      size="md"
      centered
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="Key Name"
            placeholder="Enter a descriptive name for this key"
            required
            {...form.getInputProps('keyName')}
          />

          <Textarea
            label="Description"
            placeholder="Optional description for this key"
            rows={3}
            {...form.getInputProps('description')}
          />

          <NumberInput
            label="Budget Limit (USD)"
            placeholder="Optional spending limit"
            min={0}
            step={0.01}
            decimalScale={4}
            fixedDecimalScale={false}
            thousandSeparator=","
            {...form.getInputProps('maxBudget')}
          />

          <Switch
            label="Enable key"
            description="Whether this key should be active and able to make requests"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Alert icon={<IconAlertCircle size={16} />} color="blue" variant="light">
            <Stack gap="xs">
              <Text size="sm">
                <strong>Key Hash:</strong> {virtualKey.keyHash}
              </Text>
              <Text size="sm">
                <strong>Current Spend:</strong> ${virtualKey.currentSpend.toFixed(2)}
              </Text>
              <Text size="sm">
                <strong>Total Requests:</strong> {virtualKey.requestCount.toLocaleString()}
              </Text>
              {virtualKey.lastUsed && (
                <Text size="sm">
                  <strong>Last Used:</strong> {new Date(virtualKey.lastUsed).toLocaleString()}
                </Text>
              )}
            </Stack>
          </Alert>

          <Group justify="flex-end" mt="md">
            <Button 
              variant="subtle" 
              onClick={handleClose}
              disabled={updateVirtualKey.isPending}
            >
              Cancel
            </Button>
            <Button 
              type="submit" 
              loading={updateVirtualKey.isPending}
              disabled={!form.isValid()}
            >
              Save Changes
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
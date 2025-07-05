'use client';

import {
  TextInput,
  NumberInput,
  Switch,
  Stack,
  Text,
  Textarea,
  Alert,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useUpdateVirtualKey } from '@/hooks/api/useAdminApi';
import { IconAlertCircle } from '@tabler/icons-react';
import { FormModal } from '@/components/common/FormModal';
import { validators } from '@/lib/utils/form-validators';

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

  if (!virtualKey) {
    return null;
  }

  // Create a mutation wrapper that handles the payload transformation
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const mutation: any = {
    ...updateVirtualKey,
    mutate: (values: EditVirtualKeyForm, options?: Parameters<typeof updateVirtualKey.mutate>[1]) => {
      const payload = {
        keyName: values.keyName.trim(),
        description: values.description?.trim() || undefined,
        maxBudget: values.maxBudget || undefined,
        isEnabled: values.isEnabled,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels.join(',') : undefined,
      };
      updateVirtualKey.mutate({
        id: virtualKey.id,
        data: payload,
      }, options);
    },
    mutateAsync: async (values: EditVirtualKeyForm) => {
      const payload = {
        keyName: values.keyName.trim(),
        description: values.description?.trim() || undefined,
        maxBudget: values.maxBudget || undefined,
        isEnabled: values.isEnabled,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels.join(',') : undefined,
      };
      return updateVirtualKey.mutateAsync({
        id: virtualKey.id,
        data: payload,
      });
    },
  };

  return (
    <FormModal
      opened={opened}
      onClose={onClose}
      title="Edit Virtual Key"
      size="md"
      form={form}
      mutation={mutation}
      entityType="Virtual Key"
      isEdit={true}
      submitText="Save Changes"
      initialValues={{
        keyName: virtualKey.keyName,
        description: virtualKey.description || '',
        maxBudget: virtualKey.maxBudget,
        isEnabled: virtualKey.isEnabled,
        allowedModels: virtualKey.allowedModels || [],
      }}
    >
      {(form) => (
        <>
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
        </>
      )}
    </FormModal>
  );
}
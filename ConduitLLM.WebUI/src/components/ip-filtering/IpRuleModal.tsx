'use client';

import {
  Modal,
  TextInput,
  Select,
  Textarea,
  Button,
  Group,
  Stack,
  Alert,
  Text,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertCircle } from '@tabler/icons-react';
import type { IpRule } from '@/hooks/useSecurityApi';

interface IpRuleModalProps {
  opened: boolean;
  onClose: () => void;
  onSubmit: (values: Partial<IpRule>) => Promise<void>;
  rule?: IpRule | null;
  isLoading?: boolean;
}

const validateIpAddress = (value: string) => {
  // Basic validation for IP address or CIDR notation
  const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$/;
  const cidrRegex = /^(\d{1,3}\.){3}\d{1,3}\/\d{1,2}$/;
  
  if (!ipRegex.test(value) && !cidrRegex.test(value)) {
    return 'Invalid IP address or CIDR format (e.g., 192.168.1.1 or 192.168.1.0/24)';
  }
  
  // Validate IP octets
  const parts = value.split('/')[0].split('.');
  for (const part of parts) {
    const num = parseInt(part, 10);
    if (num < 0 || num > 255) {
      return 'Each IP octet must be between 0 and 255';
    }
  }
  
  // Validate CIDR suffix if present
  if (value.includes('/')) {
    const cidrSuffix = parseInt(value.split('/')[1], 10);
    if (cidrSuffix < 0 || cidrSuffix > 32) {
      return 'CIDR suffix must be between 0 and 32';
    }
  }
  
  return null;
};

export function IpRuleModal({ 
  opened, 
  onClose, 
  onSubmit, 
  rule, 
  isLoading = false 
}: IpRuleModalProps) {
  const isEditing = !!rule;
  
  const form = useForm({
    initialValues: {
      ipAddress: rule?.ipAddress ?? '',
      action: (rule?.action ?? 'block') as 'allow' | 'block',
      description: rule?.description ?? '',
    },
    validate: {
      ipAddress: validateIpAddress,
      description: (value) => {
        if (value && value.length > 500) {
          return 'Description must be 500 characters or less';
        }
        return null;
      },
    },
  });

  const handleSubmit = async (values: typeof form.values) => {
    try {
      await onSubmit({
        ...values,
        id: rule?.id,
      });
      form.reset();
      // Don't close here - let the parent handle it after successful save
    } catch (error) {
      // Re-throw so parent can handle the error properly
      console.error('Failed to save IP rule:', error);
      throw error;
    }
  };

  const handleClose = () => {
    form.reset();
    onClose();
  };

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title={isEditing ? 'Edit IP Rule' : 'Add IP Rule'}
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="IP Address / CIDR"
            placeholder="e.g., 192.168.1.1 or 10.0.0.0/24"
            required
            {...form.getInputProps('ipAddress')}
            disabled={isEditing}
          />

          {isEditing && (
            <Alert icon={<IconAlertCircle size={16} />} color="blue">
              <Text size="sm">
                IP addresses cannot be changed after creation. 
                To use a different IP address, please create a new rule.
              </Text>
            </Alert>
          )}

          <Select
            label="Action"
            data={[
              { value: 'allow', label: 'Allow (Whitelist)' },
              { value: 'block', label: 'Block (Blacklist)' },
            ]}
            required
            {...form.getInputProps('action')}
          />

          <Textarea
            label="Description"
            placeholder="Optional description for this rule"
            rows={3}
            {...form.getInputProps('description')}
          />

          <Group justify="flex-end" mt="md">
            <Button variant="light" onClick={handleClose} disabled={isLoading}>
              Cancel
            </Button>
            <Button type="submit" loading={isLoading}>
              {isEditing ? 'Update Rule' : 'Add Rule'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
'use client';

import {
  Modal,
  Stack,
  TextInput,
  NumberInput,
  Button,
  Group,
  Text,
  Alert,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle, IconLayersLinked } from '@tabler/icons-react';
import { useState } from 'react';
import type { CreateVirtualKeyGroupRequestDto } from '@knn_labs/conduit-admin-client';

interface CreateVirtualKeyGroupModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

export function CreateVirtualKeyGroupModal({ opened, onClose, onSuccess }: CreateVirtualKeyGroupModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  const form = useForm<CreateVirtualKeyGroupRequestDto>({
    initialValues: {
      groupName: '',
      externalGroupId: '',
      initialBalance: 0,
    },
    validate: {
      groupName: (value) => (!value?.trim() ? 'Group name is required' : null),
      initialBalance: (value) => {
        if (value === undefined || value === null) return null;
        if (value < 0) return 'Initial balance cannot be negative';
        return null;
      },
    },
  });

  const handleSubmit = async (values: CreateVirtualKeyGroupRequestDto) => {
    try {
      setIsSubmitting(true);

      const response = await fetch('/api/virtualkeys/groups', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          ...values,
          externalGroupId: values.externalGroupId?.trim() ?? undefined,
        }),
      });

      if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'Failed to create virtual key group');
      }

      notifications.show({
        title: 'Success',
        message: 'Virtual key group created successfully',
        color: 'green',
      });

      form.reset();
      onClose();
      onSuccess?.();
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to create virtual key group',
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
      title={
        <Group gap="sm">
          <IconLayersLinked size={20} />
          <Text fw={500}>Create Virtual Key Group</Text>
        </Group>
      }
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="Group Name"
            placeholder="Enter a name for this group"
            required
            {...form.getInputProps('groupName')}
          />

          <TextInput
            label="External Group ID"
            placeholder="Optional external identifier"
            {...form.getInputProps('externalGroupId')}
          />

          <NumberInput
            label="Initial Balance"
            placeholder="0.00"
            prefix="$"
            min={0}
            decimalScale={2}
            fixedDecimalScale
            thousandSeparator=","
            {...form.getInputProps('initialBalance')}
          />

          <Alert icon={<IconAlertCircle size={16} />} color="blue">
            <Text size="sm">
              Virtual keys in this group will share the group&apos;s balance. 
              You can add more credits later.
            </Text>
          </Alert>

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={onClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" loading={isSubmitting}>
              Create Group
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
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
import { IconAlertCircle, IconLayersLinked } from '@tabler/icons-react';
import type { CreateVirtualKeyGroupRequestDto } from '@/lib/admin-api';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';

interface CreateVirtualKeyGroupModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

export function CreateVirtualKeyGroupModal({ opened, onClose, onSuccess }: CreateVirtualKeyGroupModalProps) {
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

  const { loading, handleSubmit, handleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction: (values) =>
      withAdminClient(client =>
        client.virtualKeyGroups.create({
          ...values,
          externalGroupId: values.externalGroupId?.trim() ?? undefined,
        })
      ),
    successMessage: 'Virtual key group created successfully',
  });

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
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
            <Button variant="subtle" onClick={handleClose} disabled={loading}>
              Cancel
            </Button>
            <Button type="submit" loading={loading}>
              Create Group
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

'use client';

import {
  Modal,
  Stack,
  NumberInput,
  TextInput,
  Button,
  Group,
  Text,
  Alert,
  Card,
  Badge,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconCash, IconAlertCircle } from '@tabler/icons-react';
import { formatters } from '@/lib/utils/formatters';
import type { VirtualKeyGroupDto, AdjustBalanceDto } from '@/lib/admin-api';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';

interface AddCreditsModalProps {
  opened: boolean;
  onClose: () => void;
  group: VirtualKeyGroupDto | null;
  onSuccess?: () => void;
}

export function AddCreditsModal({ opened, onClose, group, onSuccess }: AddCreditsModalProps) {
  const form = useForm<AdjustBalanceDto>({
    initialValues: {
      amount: 0,
      description: '',
    },
    validate: {
      amount: (value) => {
        if (!value || value <= 0) return 'Amount must be greater than 0';
        if (value > 1000000) return 'Amount cannot exceed $1,000,000';
        return null;
      },
    },
  });

  const { loading, handleSubmit, handleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction: (values) => {
      if (!group) return Promise.resolve();
      return withAdminClient(client =>
        client.virtualKeyGroups.adjustBalance(group.id, values)
      );
    },
    successMessage: 'Credits added successfully',
  });

  if (!group) return null;

  const newBalance = group.balance + (form.values.amount || 0);
  const getBalanceColor = (balance: number) => {
    if (balance <= 0) return 'red';
    if (balance < 10) return 'orange';
    return 'green';
  };

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title={
        <Group gap="sm">
          <IconCash size={20} />
          <Text fw={500}>Add Credits</Text>
        </Group>
      }
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <Card withBorder>
            <Stack gap="sm">
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Group</Text>
                <Text fw={500}>{group.groupName}</Text>
              </Group>

              <Group justify="space-between">
                <Text size="sm" c="dimmed">Current Balance</Text>
                <Badge
                  color={getBalanceColor(group.balance)}
                  variant={group.balance <= 0 ? 'filled' : 'light'}
                >
                  {formatters.currency(group.balance)}
                </Badge>
              </Group>
            </Stack>
          </Card>

          <NumberInput
            label="Amount to Add"
            placeholder="0.00"
            prefix="$"
            min={0.01}
            max={1000000}
            decimalScale={2}
            fixedDecimalScale
            thousandSeparator=","
            required
            autoFocus
            {...form.getInputProps('amount')}
          />

          <TextInput
            label="Description"
            placeholder="Reason for adding credits (optional)"
            {...form.getInputProps('description')}
          />

          {form.values.amount > 0 && (
            <Alert icon={<IconAlertCircle size={16} />} color="blue">
              <Stack gap={4}>
                <Text size="sm">New balance after adding credits:</Text>
                <Text size="lg" fw={700} c={getBalanceColor(newBalance)}>
                  {formatters.currency(newBalance)}
                </Text>
              </Stack>
            </Alert>
          )}

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={handleClose} disabled={loading}>
              Cancel
            </Button>
            <Button
              type="submit"
              loading={loading}
              leftSection={<IconCash size={16} />}
            >
              Add Credits
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

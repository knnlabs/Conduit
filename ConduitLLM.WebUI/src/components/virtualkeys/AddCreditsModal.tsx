'use client';

import {
  Modal,
  Stack,
  NumberInput,
  Button,
  Group,
  Text,
  Alert,
  Card,
  Badge,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconCash, IconAlertCircle } from '@tabler/icons-react';
import { useState } from 'react';
import { formatters } from '@/lib/utils/formatters';
import type { VirtualKeyGroupDto, AdjustBalanceDto } from '@knn_labs/conduit-admin-client';

interface AddCreditsModalProps {
  opened: boolean;
  onClose: () => void;
  group: VirtualKeyGroupDto | null;
  onSuccess?: () => void;
}

export function AddCreditsModal({ opened, onClose, group, onSuccess }: AddCreditsModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  const form = useForm<AdjustBalanceDto>({
    initialValues: {
      amount: 0,
    },
    validate: {
      amount: (value) => {
        if (!value || value <= 0) return 'Amount must be greater than 0';
        if (value > 1000000) return 'Amount cannot exceed $1,000,000';
        return null;
      },
    },
  });

  const handleSubmit = async (values: AdjustBalanceDto) => {
    if (!group) return;

    try {
      setIsSubmitting(true);

      const response = await fetch(`/api/virtualkeys/groups/${group.id}/credits`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(values),
      });

      if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'Failed to add credits');
      }

      notifications.show({
        title: 'Success',
        message: `Added ${formatters.currency(values.amount)} to ${group.groupName}`,
        color: 'green',
      });

      form.reset();
      onClose();
      onSuccess?.();
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to add credits',
        color: 'red',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

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
      onClose={onClose}
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
            <Button variant="subtle" onClick={onClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button 
              type="submit" 
              loading={isSubmitting}
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
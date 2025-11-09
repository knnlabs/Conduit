'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, NumberInput, Select, Switch, Button, Stack, Group, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type { ProviderTool, UpdateProviderTool } from '@knn_labs/conduit-admin-client';

interface EditProviderToolModalProps {
  isOpen: boolean;
  tool: ProviderTool;
  onClose: () => void;
  onSuccess: () => void;
}

export function EditProviderToolModal({ isOpen, tool, onClose, onSuccess }: EditProviderToolModalProps) {
  const { executeWithAdmin } = useAdminClient();
  const [billingUnits, setBillingUnits] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);

  const form = useForm<UpdateProviderTool>({
    initialValues: {
      isActive: tool.isActive,
      toolParameters: tool.toolParameters ?? null,
      costPerUnit: tool.costPerUnit ?? 0,
      billingUnit: tool.billingUnit ?? '',
      costDescription: tool.costDescription ?? '',
    },
    validate: {
      costPerUnit: (value) => value !== null && value !== undefined && value < 0 ? 'Cost must be non-negative' : null,
    },
  });

  useEffect(() => {
    const loadBillingUnits = async () => {
      try {
        const units = await executeWithAdmin(client => client.providerTools.getBillingUnits());
        setBillingUnits(units);
      } catch (error) {
        console.error('Failed to load billing units:', error);
      }
    };

    if (isOpen) {
      void loadBillingUnits();
      // Reset form when tool changes
      form.setValues({
        isActive: tool.isActive,
        toolParameters: tool.toolParameters ?? null,
        costPerUnit: tool.costPerUnit ?? 0,
        billingUnit: tool.billingUnit ?? '',
        costDescription: tool.costDescription ?? '',
      });
    }
  }, [isOpen, tool, form, executeWithAdmin]);

  const handleSubmit = async (values: UpdateProviderTool) => {
    try {
      setLoading(true);
      await executeWithAdmin(client =>
        client.providerTools.updateProviderTool(tool.id, {
          ...values,
          toolParameters: values.toolParameters?.trim() ?? null,
          costDescription: values.costDescription?.trim() ?? null,
          billingUnit: values.billingUnit?.trim() ?? null,
        })
      );
      notifications.show({
        title: 'Tool Updated',
        message: `Successfully updated ${tool.toolName}`,
        color: 'green',
      });
      onSuccess();
    } catch (error) {
      console.error('Failed to update tool:', error);
      notifications.show({
        title: 'Update Failed',
        message: error instanceof Error ? error.message : 'Failed to update provider tool',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={`Edit ${tool.toolName}`}
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack>
          <TextInput
            label="Provider"
            value={tool.providerName ?? 'Unknown'}
            disabled
          />

          <TextInput
            label="Tool Name"
            value={tool.toolName}
            disabled
          />

          <Textarea
            label="Tool Parameters"
            placeholder="Optional JSON parameters"
            minRows={2}
            {...form.getInputProps('toolParameters')}
          />

          <NumberInput
            label="Cost Per Unit"
            placeholder="0.0001"
            min={0}
            decimalScale={8}
            step={0.0001}
            {...form.getInputProps('costPerUnit')}
          />

          <Select
            label="Billing Unit"
            placeholder="Select a billing unit"
            data={billingUnits}
            searchable
            {...form.getInputProps('billingUnit')}
          />

          <Textarea
            label="Cost Description"
            placeholder="Describe how the cost is calculated"
            {...form.getInputProps('costDescription')}
          />

          <Switch
            label="Active"
            {...form.getInputProps('isActive', { type: 'checkbox' })}
          />

          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={loading}>
              Update Tool
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
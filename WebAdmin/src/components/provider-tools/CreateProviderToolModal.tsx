'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, NumberInput, Select, Switch, Button, Stack, Group, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type { CreateProviderTool, ToolProviderOption } from '@knn_labs/conduit-admin-client';

interface CreateProviderToolModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function CreateProviderToolModal({ isOpen, onClose, onSuccess }: CreateProviderToolModalProps) {
  const { executeWithAdmin } = useAdminClient();
  const [providers, setProviders] = useState<ToolProviderOption[]>([]);
  const [billingUnits, setBillingUnits] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);

  const form = useForm<CreateProviderTool>({
    initialValues: {
      provider: 0,
      toolName: '',
      toolParameters: null,
      costPerUnit: 0,
      billingUnit: '',
      costDescription: '',
      isActive: true,
    },
    validate: {
      provider: (value) => value === 0 ? 'Provider is required' : null,
      toolName: (value) => !value?.trim() ? 'Tool name is required' : null,
      costPerUnit: (value) => value !== null && value !== undefined && value < 0 ? 'Cost must be non-negative' : null,
    },
  });

  useEffect(() => {
    const loadOptions = async () => {
      try {
        const [providersData, unitsData] = await Promise.all([
          executeWithAdmin(client => client.providerTools.getToolProviders()),
          executeWithAdmin(client => client.providerTools.getBillingUnits()),
        ]);
        setProviders(providersData);
        setBillingUnits(unitsData);
      } catch (error) {
        console.error('Failed to load options:', error);
        notifications.show({
          title: 'Failed to Load Options',
          message: 'Could not load provider and billing unit options',
          color: 'red',
        });
      }
    };

    if (isOpen) {
      void loadOptions();
    }
  }, [isOpen, executeWithAdmin]);

  const handleSubmit = async (values: CreateProviderTool) => {
    try {
      setLoading(true);
      await executeWithAdmin(client =>
        client.providerTools.createProviderTool({
          ...values,
          toolParameters: values.toolParameters?.trim() ?? null,
          costDescription: values.costDescription?.trim() ?? null,
          billingUnit: values.billingUnit?.trim() ?? null,
        })
      );
      notifications.show({
        title: 'Tool Created',
        message: `Successfully created ${values.toolName}`,
        color: 'green',
      });
      form.reset();
      onSuccess();
    } catch (error) {
      console.error('Failed to create tool:', error);
      notifications.show({
        title: 'Creation Failed',
        message: error instanceof Error ? error.message : 'Failed to create provider tool',
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
      title="Add Provider Tool"
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack>
          <Select
            label="Provider"
            placeholder="Select a provider"
            data={providers.map(p => ({
              value: p.value.toString(),
              label: p.name,
            }))}
            {...form.getInputProps('provider')}
            onChange={(value) => form.setFieldValue('provider', value ? parseInt(value) : 0)}
            value={form.values.provider.toString()}
            required
          />

          <TextInput
            label="Tool Name"
            placeholder="e.g., code_execution, web_search"
            {...form.getInputProps('toolName')}
            required
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
              Create Tool
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
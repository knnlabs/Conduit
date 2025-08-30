'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Stack, Group } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type { CreateModelDto, ModelSeriesDto } from '@knn_labs/conduit-admin-client';

interface CreateModelModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}


export function CreateModelModal({ isOpen, onClose, onSuccess }: CreateModelModalProps) {
  const [loading, setLoading] = useState(false);
  const [series, setSeries] = useState<ModelSeriesDto[]>([]);
  // Capabilities are now embedded in the Model, no need for separate capabilities
  const { executeWithAdmin } = useAdminClient();

  const form = useForm({
    initialValues: {
      name: '',
      modelSeriesId: '',
      isActive: true
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null
    }
  });

  useEffect(() => {
    if (isOpen) {
      void loadData();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  const loadData = async () => {
    try {
      const seriesData = await executeWithAdmin(client => client.modelSeries.list());
      setSeries(seriesData);
      // Capabilities are now embedded in the Model, no need to load separately
    } catch (error) {
      console.error('Failed to load data:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load series data',
        color: 'red',
      });
    }
  };

  const handleSubmit = async (values: typeof form.values) => {
    try {
      setLoading(true);
      const dto: CreateModelDto = {
        name: values.name,
        modelSeriesId: values.modelSeriesId ? parseInt(values.modelSeriesId) : undefined,
        isActive: values.isActive
      };
      await executeWithAdmin(client => client.models.create(dto));
      notifications.show({
        title: 'Success',
        message: 'Model created successfully',
        color: 'green',
      });
      form.reset();
      onSuccess();
    } catch (error) {
      console.error('Failed to create model:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to create model',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  const seriesOptions = series.map(s => ({
    value: s.id?.toString() ?? '',
    label: `${s.name ?? 'Unnamed'} ${s.authorName ? `(${s.authorName})` : ''}`
  }));

  // Capabilities are now embedded in the Model, no separate selection needed

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Create New Model"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack>
          <TextInput
            label="Model Name"
            placeholder="e.g., gpt-4-turbo"
            required
            {...form.getInputProps('name')}
          />

          <Select
            label="Model Series"
            data={seriesOptions}
            placeholder="Select a series (optional)"
            {...form.getInputProps('modelSeriesId')}
          />

          {/* Capabilities are now embedded directly in the Model entity */}

          <Switch
            label="Active"
            {...form.getInputProps('isActive', { type: 'checkbox' })}
          />

          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={loading}>
              Create Model
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
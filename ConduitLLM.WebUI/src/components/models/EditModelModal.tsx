'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Stack, Group } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import type { ModelDto, UpdateModelDto, ModelSeriesDto, ModelCapabilitiesDto } from '@knn_labs/conduit-admin-client';


interface EditModelModalProps {
  isOpen: boolean;
  model: ModelDto;
  onClose: () => void;
  onSuccess: () => void;
}

const MODEL_TYPES = [
  { value: '0', label: 'Text/Chat' },
  { value: '1', label: 'Image Generation' },
  { value: '2', label: 'Audio (TTS/STT)' },
  { value: '3', label: 'Video Generation' },
  { value: '4', label: 'Embedding' }
];

export function EditModelModal({ isOpen, model, onClose, onSuccess }: EditModelModalProps) {
  const [loading, setLoading] = useState(false);
  const [series, setSeries] = useState<ModelSeriesDto[]>([]);
  const [capabilities, setCapabilities] = useState<ModelCapabilitiesDto[]>([]);
  const { executeWithAdmin } = useAdminClient();

  const form = useForm<UpdateModelDto>({
    initialValues: {
      name: model?.name ?? '',
      modelType: model?.modelType ?? 0,
      modelSeriesId: model?.modelSeriesId ?? null,
      modelCapabilitiesId: model?.modelCapabilitiesId ?? null,
      isActive: model?.isActive ?? true
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null,
      modelType: (value) => value === undefined ? 'Type is required' : null
    }
  });

  useEffect(() => {
    if (model) {
      form.setValues({
        name: model.name ?? '',
        modelType: model.modelType ?? 0,
        modelSeriesId: model.modelSeriesId ?? null,
        modelCapabilitiesId: model.modelCapabilitiesId ?? null,
        isActive: model.isActive ?? true
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [model]);

  useEffect(() => {
    if (isOpen) {
      void loadData();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  const loadData = async () => {
    try {
      const [seriesData, capabilitiesData] = await Promise.all([
        executeWithAdmin(client => client.modelSeries.list()),
        executeWithAdmin(client => client.modelCapabilities.list())
      ]);
      setSeries(seriesData);
      // Fix type incompatibility by ensuring correct types
      setCapabilities(capabilitiesData as ModelCapabilitiesDto[]);
    } catch (error) {
      console.error('Failed to load data:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load series and capabilities',
        color: 'red',
      });
    }
  };

  const handleSubmit = async (values: UpdateModelDto) => {
    try {
      setLoading(true);
      const modelId = model.id;
      if (!modelId) throw new Error('Model ID is required');
      await executeWithAdmin(client => client.models.update(modelId, values));
      notifications.show({
        title: 'Success',
        message: 'Model updated successfully',
        color: 'green',
      });
      onSuccess();
    } catch (error) {
      console.error('Failed to update model:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to update model',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  const seriesOptions = series
    .filter(s => s.id !== undefined)
    .map(s => ({
      value: s.id?.toString() ?? '',
      label: `${s.name} (${s.authorName})`
    }));

  const capabilitiesOptions = [
    { value: '0', label: 'None' },
    ...capabilities
      .filter(c => c.id !== undefined)
      .map(c => ({
        value: c.id?.toString() ?? '',
        label: `Capability ${c.id}`
      }))
  ];

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Edit Model"
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
            label="Model Type"
            required
            data={MODEL_TYPES}
            value={form.values.modelType?.toString() ?? '0'}
            onChange={(value) => {
              const numValue = value ? parseInt(value) : 0;
              form.setFieldValue('modelType', numValue as 0 | 1 | 2 | 3 | 4);
            }}
          />

          <Select
            label="Model Series"
            required
            data={seriesOptions}
            placeholder="Select a series"
            value={form.values.modelSeriesId?.toString() ?? ''}
            onChange={(value) => form.setFieldValue('modelSeriesId', value ? parseInt(value) : null)}
          />

          <Select
            label="Capabilities"
            data={capabilitiesOptions}
            placeholder="Select capabilities (optional)"
            value={form.values.modelCapabilitiesId?.toString() ?? '0'}
            onChange={(value) => {
              const id = value ? parseInt(value) : 0;
              form.setFieldValue('modelCapabilitiesId', id === 0 ? null : id);
            }}
          />






          <Group>
            <Switch
              label="Active"
              {...form.getInputProps('isActive', { type: 'checkbox' })}
            />
          </Group>

          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={loading}>
              Update Model
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
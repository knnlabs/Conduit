'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Stack, Group, Textarea, Alert, Text } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { ParameterPreview } from '@/components/parameters/ParameterPreview';
import type { ModelDto, UpdateModelDto, ModelSeriesDto, ModelCapabilitiesDto } from '@knn_labs/conduit-admin-client';

// Extend ModelDto to include modelParameters until SDK types are updated
interface ExtendedModelDto extends ModelDto {
  modelParameters?: string | null;
}

// Extend UpdateModelDto to include modelParameters until SDK types are updated
interface ExtendedUpdateModelDto extends UpdateModelDto {
  modelParameters?: string | null;
}


interface EditModelModalProps {
  isOpen: boolean;
  model: ExtendedModelDto;
  onClose: () => void;
  onSuccess: () => void;
}


export function EditModelModal({ isOpen, model, onClose, onSuccess }: EditModelModalProps) {
  const [loading, setLoading] = useState(false);
  const [series, setSeries] = useState<ModelSeriesDto[]>([]);
  const [capabilities, setCapabilities] = useState<ModelCapabilitiesDto[]>([]);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const { executeWithAdmin } = useAdminClient();

  const form = useForm<ExtendedUpdateModelDto>({
    initialValues: {
      name: model?.name ?? '',
      modelSeriesId: model?.modelSeriesId ?? null,
      modelCapabilitiesId: model?.modelCapabilitiesId ?? null,
      isActive: model?.isActive ?? true,
      modelParameters: model?.modelParameters ?? ''
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null,
      modelParameters: (value) => {
        if (value) {
          try {
            JSON.parse(value);
            setJsonError(null);
            return null;
          } catch {
            const error = 'Invalid JSON format';
            setJsonError(error);
            return error;
          }
        }
        return null;
      }
    }
  });

  useEffect(() => {
    if (model) {
      form.setValues({
        name: model.name ?? '',
        modelSeriesId: model.modelSeriesId ?? null,
        modelCapabilitiesId: model.modelCapabilitiesId ?? null,
        isActive: model.isActive ?? true,
        modelParameters: model.modelParameters ?? ''
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

  const handleSubmit = async (values: ExtendedUpdateModelDto) => {
    try {
      setLoading(true);
      const modelId = model.id;
      if (!modelId) throw new Error('Model ID is required');
      
      const dto: ExtendedUpdateModelDto = {
        name: values.name,
        modelSeriesId: values.modelSeriesId,
        modelCapabilitiesId: values.modelCapabilitiesId,
        isActive: values.isActive,
        modelParameters: values.modelParameters ?? null
      };
      
      // Cast to unknown first to bypass type checking until SDK is updated
      await executeWithAdmin(client => client.models.update(modelId, dto as unknown as UpdateModelDto));
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

  const validateJson = (value: string) => {
    try {
      if (value) {
        JSON.parse(value);
        setJsonError(null);
      }
    } catch {
      setJsonError('Invalid JSON format');
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






          <Stack gap="xs">
            <Text size="sm" fw={500}>
              Model Parameters (JSON)
            </Text>
            
            <Text size="xs" c="dimmed">
              Optional: Override series-level parameters for this specific model
            </Text>
            
            <ParameterPreview 
              parametersJson={form.values.modelParameters ?? ''}
              context="chat"
              label="Preview UI Components"
              maxHeight={300}
            />
            
            <Textarea
              placeholder="JSON parameters for UI generation (leave empty to use series defaults)..."
              rows={8}
              style={{ fontFamily: 'monospace' }}
              {...form.getInputProps('modelParameters')}
              onChange={(e) => {
                form.setFieldValue('modelParameters', e.currentTarget.value);
                validateJson(e.currentTarget.value);
              }}
              error={jsonError}
            />

            {jsonError && (
              <Alert icon={<IconAlertCircle size={16} />} color="red" variant="light">
                {jsonError}
              </Alert>
            )}
          </Stack>

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
            <Button type="submit" loading={loading} disabled={!!jsonError}>
              Update Model
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
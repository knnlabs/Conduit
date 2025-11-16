'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Stack, Group, NumberInput, Divider } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import { TOKENIZER_SELECT_OPTIONS, TokenizerType } from '@/lib/utils/tokenizerTypes';
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
      tokenizerType: TokenizerType.Cl100KBase,
      isActive: true,
      supportsChat: true,
      supportsVision: false,
      supportsFunctionCalling: false,
      supportsStreaming: true,
      supportsImageGeneration: false,
      supportsVideoGeneration: false,
      supportsEmbeddings: false,
      maxInputTokens: undefined as number | undefined,
      maxOutputTokens: undefined as number | undefined
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null,
      tokenizerType: (value: TokenizerType | null | undefined) => {
        if (value === null || value === undefined) return 'Tokenizer type is required';
        if (typeof value !== 'number') return 'Invalid tokenizer type';
        const isValidEnum = Object.values(TokenizerType)
          .filter((v): v is number => typeof v === 'number')
          .includes(value);
        if (!isValidEnum) return 'Invalid tokenizer type';
        return null;
      }
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

  const handleClose = () => {
    form.reset();
    onClose();
  };

  const handleSubmit = async (values: typeof form.values) => {
    try {
      setLoading(true);
      const dto: CreateModelDto = {
        name: values.name,
        modelSeriesId: values.modelSeriesId ? parseInt(values.modelSeriesId) : undefined,
        tokenizerType: values.tokenizerType,
        isActive: values.isActive,
        supportsChat: values.supportsChat,
        supportsVision: values.supportsVision,
        supportsFunctionCalling: values.supportsFunctionCalling,
        supportsStreaming: values.supportsStreaming,
        supportsImageGeneration: values.supportsImageGeneration,
        supportsVideoGeneration: values.supportsVideoGeneration,
        supportsEmbeddings: values.supportsEmbeddings,
        maxInputTokens: values.maxInputTokens ?? undefined,
        maxOutputTokens: values.maxOutputTokens ?? undefined
      } as CreateModelDto;
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
      onClose={handleClose}
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

          <Select
            label="Tokenizer Type"
            data={TOKENIZER_SELECT_OPTIONS}
            placeholder="Select tokenizer type"
            value={form.values.tokenizerType.toString()}
            onChange={(value) => form.setFieldValue('tokenizerType', value ? parseInt(value) : TokenizerType.Cl100KBase)}
            required
            searchable
            error={form.errors.tokenizerType}
          />

          <Divider label="Model Capabilities" labelPosition="center" my="md" />
          
          <Group grow>
            <Switch
              label="Supports Chat"
              {...form.getInputProps('supportsChat', { type: 'checkbox' })}
            />
            <Switch
              label="Supports Vision"
              {...form.getInputProps('supportsVision', { type: 'checkbox' })}
            />
          </Group>

          <Group grow>
            <Switch
              label="Supports Function Calling"
              {...form.getInputProps('supportsFunctionCalling', { type: 'checkbox' })}
            />
            <Switch
              label="Supports Streaming"
              {...form.getInputProps('supportsStreaming', { type: 'checkbox' })}
            />
          </Group>

          <Group grow>
            <Switch
              label="Supports Image Generation"
              {...form.getInputProps('supportsImageGeneration', { type: 'checkbox' })}
            />
            <Switch
              label="Supports Video Generation"
              {...form.getInputProps('supportsVideoGeneration', { type: 'checkbox' })}
            />
          </Group>

          <Switch
            label="Supports Embeddings"
            {...form.getInputProps('supportsEmbeddings', { type: 'checkbox' })}
          />

          <Divider label="Token Limits" labelPosition="center" my="md" />

          <Group grow>
            <NumberInput
              label="Max Input Tokens"
              placeholder="e.g., 128000"
              min={0}
              {...form.getInputProps('maxInputTokens')}
            />
            <NumberInput
              label="Max Output Tokens"
              placeholder="e.g., 4096"
              min={0}
              {...form.getInputProps('maxOutputTokens')}
            />
          </Group>

          <Divider my="md" />

          <Switch
            label="Active"
            {...form.getInputProps('isActive', { type: 'checkbox' })}
          />

          <Group justify="flex-end">
            <Button variant="subtle" onClick={handleClose}>
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
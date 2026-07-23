'use client';

import { useState, useEffect } from 'react';
import { TextInput, Select, Switch, Group, NumberInput, Divider, MultiSelect, Text } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';
import { EntityFormModal } from '@/components/common/EntityFormModal';
import { TOKENIZER_SELECT_OPTIONS, TokenizerType, isValidTokenizerType } from '@/lib/utils/tokenizerTypes';
import type { CreateModelDto, ModelSeriesDto } from '@/lib/admin-api';

interface CreateModelModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

const MODALITY_OPTIONS = ['text', 'image', 'audio', 'video', 'file'];

export function CreateModelModal({ isOpen, onClose, onSuccess }: CreateModelModalProps) {
  const [series, setSeries] = useState<ModelSeriesDto[]>([]);
  // Capabilities are now embedded in the Model, no need for separate capabilities

  const form = useForm({
    initialValues: {
      name: '',
      modelSeriesId: '',
      tokenizerType: TokenizerType.Cl100KBase as TokenizerType,
      isActive: true,
      capabilitiesKnown: true,
      inputModalities: ['text'] as string[],
      outputModalities: ['text'] as string[],
      supportsChat: true,
      supportsVision: false,
      supportsFunctionCalling: false,
      supportsStreaming: true,
      supportsImageGeneration: false,
      supportsVideoGeneration: false,
      supportsEmbeddings: false,
      supportsSpeechToText: false,
      supportsTextToSpeech: false,
      supportsRerank: false,
      maxInputTokens: undefined as number | undefined,
      maxOutputTokens: undefined as number | undefined
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null,
      tokenizerType: (value: TokenizerType | null | undefined) => {
        if (value === null || value === undefined) return 'Tokenizer type is required';
        if (!isValidTokenizerType(value)) return 'Invalid tokenizer type';
        return null;
      }
    }
  });

  useEffect(() => {
    if (isOpen) {
      void loadData();
    }
  }, [isOpen]);

  const loadData = async () => {
    try {
      const seriesData = await withAdminClient(client => client.modelSeries.list());
      setSeries(seriesData);
      // Capabilities are now embedded in the Model, no need to load separately
    } catch (error) {
      console.error('Failed to load data:', error);
      notify.error(error, 'Failed to load series data');
    }
  };

  const { loading, handleSubmit, handleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction: async (values) => {
      const dto: CreateModelDto = {
        name: values.name,
        modelSeriesId: values.modelSeriesId ? parseInt(values.modelSeriesId) : undefined,
        tokenizerType: values.tokenizerType,
        isActive: values.isActive,
        inputModalities: values.capabilitiesKnown ? values.inputModalities : null,
        outputModalities: values.capabilitiesKnown ? values.outputModalities : null,
        capabilitySource: values.capabilitiesKnown ? 'manual' : 'unknown',
        supportsChat: values.supportsChat,
        supportsVision: values.capabilitiesKnown && values.inputModalities.includes('image'),
        supportsFunctionCalling: values.supportsFunctionCalling,
        supportsStreaming: values.supportsStreaming,
        supportsImageGeneration: values.supportsImageGeneration,
        supportsVideoGeneration: values.supportsVideoGeneration,
        supportsEmbeddings: values.supportsEmbeddings,
        supportsSpeechToText: values.supportsSpeechToText,
        supportsTextToSpeech: values.supportsTextToSpeech,
        supportsRerank: values.supportsRerank,
        maxInputTokens: values.maxInputTokens ?? undefined,
        maxOutputTokens: values.maxOutputTokens ?? undefined
      } as CreateModelDto;
      await withAdminClient(client => client.models.create(dto));
    },
    successMessage: 'Model created successfully',
  });

  const seriesOptions = series.map(s => ({
    value: s.id?.toString() ?? '',
    label: `${s.name ?? 'Unnamed'} ${s.authorName ? `(${s.authorName})` : ''}`
  }));

  // Capabilities are now embedded in the Model, no separate selection needed

  return (
    <EntityFormModal
      opened={isOpen}
      onClose={handleClose}
      title="Create New Model"
      onSubmit={form.onSubmit(handleSubmit)}
      loading={loading}
      submitLabel="Create Model"
    >
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
        onChange={(value) => form.setFieldValue('tokenizerType', value as TokenizerType ?? TokenizerType.Cl100KBase)}
        required
        searchable
        error={form.errors.tokenizerType}
      />

      <Divider label="Directional Modalities" labelPosition="center" my="md" />

      <Switch
        label="Directional metadata is known"
        description="Turn off to store unknown. Empty selections explicitly mean unsupported."
        {...form.getInputProps('capabilitiesKnown', { type: 'checkbox' })}
      />

      {form.values.capabilitiesKnown && (
        <>
          <MultiSelect
            label="Accepted inputs"
            data={MODALITY_OPTIONS}
            searchable
            {...form.getInputProps('inputModalities')}
          />
          <MultiSelect
            label="Produced outputs"
            data={MODALITY_OPTIONS}
            searchable
            {...form.getInputProps('outputModalities')}
          />
          <Text size="xs" c="dimmed">
            Video input and video generation are independent. A model may analyze video while only producing text.
          </Text>
        </>
      )}

      <Divider label="Operations" labelPosition="center" my="md" />

      <Group grow>
        <Switch
          label="Supports Chat"
          {...form.getInputProps('supportsChat', { type: 'checkbox' })}
        />
        <Switch label="Image input (legacy vision flag)" checked={form.values.inputModalities.includes('image')} disabled />
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

      <Group grow>
        <Switch
          label="Speech to Text"
          {...form.getInputProps('supportsSpeechToText', { type: 'checkbox' })}
        />
        <Switch
          label="Text to Speech"
          {...form.getInputProps('supportsTextToSpeech', { type: 'checkbox' })}
        />
        <Switch
          label="Rerank"
          {...form.getInputProps('supportsRerank', { type: 'checkbox' })}
        />
      </Group>

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
    </EntityFormModal>
  );
}

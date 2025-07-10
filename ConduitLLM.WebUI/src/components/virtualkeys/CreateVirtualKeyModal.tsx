'use client';

import {
  Modal,
  TextInput,
  NumberInput,
  Switch,
  Button,
  Text,
  Textarea,
  Alert,
  MultiSelect,
  TagsInput,
  Divider,
  Stack,
  Group,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconInfoCircle } from '@tabler/icons-react';
import { useState } from 'react';
import { validators } from '@/lib/utils/form-validators';
import { notifications } from '@mantine/notifications';

interface CreateVirtualKeyModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

interface CreateVirtualKeyForm {
  keyName: string;
  description?: string;
  maxBudget?: number;
  rateLimitPerMinute?: number;
  isEnabled: boolean;
  allowedModels: string[];
  allowedEndpoints: string[];
  allowedIpAddresses: string[];
  metadata?: string;
}

const ENDPOINT_OPTIONS = [
  { value: '/v1/chat/completions', label: 'Chat Completions' },
  { value: '/v1/completions', label: 'Completions' },
  { value: '/v1/embeddings', label: 'Embeddings' },
  { value: '/v1/images/generations', label: 'Image Generation' },
  { value: '/v1/audio/transcriptions', label: 'Audio Transcription' },
  { value: '/v1/audio/translations', label: 'Audio Translation' },
  { value: '/v1/audio/speech', label: 'Text to Speech' },
  { value: '/v1/moderations', label: 'Moderations' },
  { value: '/v1/videos/generations', label: 'Video Generation' },
];

// Common models - hardcoded for now since we removed SDK
const MODEL_OPTIONS = [
  { value: '*', label: 'All Models' },
  { value: 'gpt-4', label: 'gpt-4' },
  { value: 'gpt-4-turbo', label: 'gpt-4-turbo' },
  { value: 'gpt-3.5-turbo', label: 'gpt-3.5-turbo' },
  { value: 'claude-3-opus', label: 'claude-3-opus' },
  { value: 'claude-3-sonnet', label: 'claude-3-sonnet' },
  { value: 'claude-3-haiku', label: 'claude-3-haiku' },
];

export function CreateVirtualKeyModal({ opened, onClose, onSuccess }: CreateVirtualKeyModalProps) {
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const form = useForm<CreateVirtualKeyForm>({
    initialValues: {
      keyName: '',
      description: '',
      maxBudget: undefined,
      rateLimitPerMinute: undefined,
      isEnabled: true,
      allowedModels: [],
      allowedEndpoints: ['/v1/chat/completions'],
      allowedIpAddresses: [],
      metadata: '',
    },
    validate: {
      keyName: (value) => {
        const requiredError = validators.required('Key name')(value);
        if (requiredError) return requiredError;
        
        const minLengthError = validators.minLength('Key name', 3)(value);
        if (minLengthError) return minLengthError;
        
        const maxLengthError = validators.maxLength('Key name', 100)(value);
        if (maxLengthError) return maxLengthError;
        
        return null;
      },
      maxBudget: validators.positiveNumber('Budget'),
      rateLimitPerMinute: validators.minValue('Rate limit', 1),
      allowedModels: validators.arrayMinLength('model', 1),
      allowedEndpoints: validators.arrayMinLength('endpoint', 1),
      allowedIpAddresses: validators.ipAddresses,
    },
  });

  const handleSubmit = async (values: CreateVirtualKeyForm) => {
    setIsSubmitting(true);
    try {
      const payload = {
        keyName: values.keyName.trim(),
        description: values.description?.trim() || undefined,
        maxBudget: values.maxBudget || undefined,
        rateLimitRpm: values.rateLimitPerMinute || undefined,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels.join(',') : undefined,
        metadata: values.metadata?.trim() ? values.metadata : undefined,
      };

      const response = await fetch('/api/virtualkeys', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        throw new Error('Failed to create virtual key');
      }

      notifications.show({
        title: 'Success',
        message: 'Virtual key created successfully',
        color: 'green',
      });
      
      handleClose();
      if (onSuccess) {
        onSuccess();
      }
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to create virtual key',
        color: 'red',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleClose = () => {
    setShowAdvanced(false);
    form.reset();
    onClose();
  };

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Create Virtual Key"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
      <TextInput
        label="Key Name"
        placeholder="Enter a unique name for this key"
        required
        {...form.getInputProps('keyName')}
      />

      <Textarea
        label="Description"
        placeholder="Optional description for this key"
        rows={3}
        {...form.getInputProps('description')}
      />

      <Switch
        label="Enabled"
        description="Whether this key can be used for API requests"
        checked={form.values.isEnabled}
        {...form.getInputProps('isEnabled')}
      />

      <NumberInput
        label="Maximum Budget"
        description="Maximum amount this key can spend (in USD)"
        placeholder="No limit"
        min={0}
        step={10}
        decimalScale={2}
        prefix="$"
        {...form.getInputProps('maxBudget')}
      />

      <Button
        variant="subtle"
        onClick={() => setShowAdvanced(!showAdvanced)}
        mb="md"
      >
        {showAdvanced ? 'Hide' : 'Show'} Advanced Settings
      </Button>

      {showAdvanced && (
        <>
          <Divider mb="md" />

          <NumberInput
            label="Rate Limit"
            description="Maximum requests per minute"
            placeholder="No limit"
            min={1}
            {...form.getInputProps('rateLimitPerMinute')}
          />

          <MultiSelect
            label="Allowed Models"
            description="Models this key can access"
            data={MODEL_OPTIONS}
            placeholder="Select models"
            searchable
            clearable
            required
            {...form.getInputProps('allowedModels')}
          />

          <MultiSelect
            label="Allowed Endpoints"
            description="API endpoints this key can access"
            data={ENDPOINT_OPTIONS}
            placeholder="Select endpoints"
            searchable
            clearable
            required
            {...form.getInputProps('allowedEndpoints')}
          />

          <TagsInput
            label="IP Whitelist"
            description="IP addresses allowed to use this key"
            placeholder="Enter IP addresses"
            {...form.getInputProps('allowedIpAddresses')}
          />

          <Textarea
            label="Metadata"
            description="Additional metadata in JSON format"
            placeholder='{"team": "engineering"}'
            rows={3}
            {...form.getInputProps('metadata')}
          />

          <Alert icon={<IconInfoCircle size={16} />} color="blue" mt="md">
            <Text size="sm">
              Advanced settings help you control access and usage. Leave empty for default values.
            </Text>
          </Alert>
        </>
      )}
      
      <Group justify="flex-end" mt="md">
        <Button variant="subtle" onClick={handleClose}>
          Cancel
        </Button>
        <Button type="submit" loading={isSubmitting}>
          Create Key
        </Button>
      </Group>
      </Stack>
      </form>
    </Modal>
  );
}
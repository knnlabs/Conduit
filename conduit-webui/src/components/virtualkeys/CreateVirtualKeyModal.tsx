'use client';

import {
  Modal,
  TextInput,
  NumberInput,
  Switch,
  Button,
  Stack,
  Group,
  Text,
  Textarea,
  Alert,
  MultiSelect,
  TagsInput,
  Divider,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useCreateVirtualKey } from '@/hooks/api/useAdminApi';
import { useAvailableModels } from '@/hooks/api/useCoreApi';
import { IconAlertCircle, IconInfoCircle } from '@tabler/icons-react';
import { useState } from 'react';

interface CreateVirtualKeyModalProps {
  opened: boolean;
  onClose: () => void;
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

export function CreateVirtualKeyModal({ opened, onClose }: CreateVirtualKeyModalProps) {
  const createVirtualKey = useCreateVirtualKey();
  const { data: availableModels } = useAvailableModels();
  const [showAdvanced, setShowAdvanced] = useState(false);

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
        if (!value || value.trim().length === 0) {
          return 'Key name is required';
        }
        if (value.length < 3) {
          return 'Key name must be at least 3 characters';
        }
        if (value.length > 100) {
          return 'Key name must be less than 100 characters';
        }
        return null;
      },
      maxBudget: (value) => {
        if (value !== undefined && value < 0) {
          return 'Budget must be positive';
        }
        return null;
      },
      rateLimitPerMinute: (value) => {
        if (value !== undefined && value < 1) {
          return 'Rate limit must be at least 1';
        }
        return null;
      },
      allowedModels: (value) => {
        if (!value || value.length === 0) {
          return 'At least one model must be selected';
        }
        return null;
      },
      allowedEndpoints: (value) => {
        if (!value || value.length === 0) {
          return 'At least one endpoint must be selected';
        }
        return null;
      },
      allowedIpAddresses: (value) => {
        // Validate IP addresses format
        const ipRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$/;
        const cidrRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\/(?:3[0-2]|[12]?[0-9])$/;
        
        for (const ip of value) {
          if (!ipRegex.test(ip) && !cidrRegex.test(ip)) {
            return `Invalid IP address or CIDR: ${ip}`;
          }
        }
        return null;
      },
    },
  });

  const handleSubmit = async (values: CreateVirtualKeyForm) => {
    try {
      const payload = {
        keyName: values.keyName.trim(),
        description: values.description?.trim() || undefined,
        maxBudget: values.maxBudget || undefined,
        rateLimitPerMinute: values.rateLimitPerMinute || undefined,
        isEnabled: values.isEnabled,
        allowedModels: values.allowedModels,
        allowedEndpoints: values.allowedEndpoints,
        allowedIpAddresses: values.allowedIpAddresses.length > 0 ? values.allowedIpAddresses : undefined,
        metadata: values.metadata?.trim() ? JSON.parse(values.metadata) : undefined,
      };

      await createVirtualKey.mutateAsync(payload);
      
      // Reset form and close modal on success
      form.reset();
      setShowAdvanced(false);
      onClose();
    } catch (error) {
      // Error is handled by the mutation hook
      console.error('Create virtual key error:', error);
    }
  };

  const handleClose = () => {
    form.reset();
    setShowAdvanced(false);
    onClose();
  };

  // Get available models for selection
  const modelOptions = availableModels?.models?.map((model: any) => ({
    value: model.id,
    label: `${model.id} (${model.provider})`,
  })) || [];

  // Add option to allow all models
  if (modelOptions.length > 0) {
    modelOptions.unshift({ value: '*', label: 'All Models' });
  }

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Create Virtual Key"
      size="lg"
      centered
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="Key Name"
            placeholder="My API Key"
            description="A descriptive name for this virtual key"
            required
            {...form.getInputProps('keyName')}
          />

          <Textarea
            label="Description"
            placeholder="Optional description for this key"
            rows={2}
            {...form.getInputProps('description')}
          />

          <Divider label="Access Control" labelPosition="left" />

          <MultiSelect
            label="Allowed Models"
            placeholder="Select models this key can access"
            description="Choose which models this key is allowed to use"
            required
            data={modelOptions}
            searchable
            {...form.getInputProps('allowedModels')}
          />

          <MultiSelect
            label="Allowed Endpoints"
            placeholder="Select allowed API endpoints"
            description="Choose which API endpoints this key can access"
            required
            data={ENDPOINT_OPTIONS}
            {...form.getInputProps('allowedEndpoints')}
          />

          <Switch
            label="Enable key"
            description="Whether this key should be active immediately"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Divider label="Limits & Restrictions" labelPosition="left" />

          <NumberInput
            label="Budget Limit (USD)"
            placeholder="No limit"
            description="Maximum spend allowed for this key"
            min={0}
            step={0.01}
            decimalScale={2}
            prefix="$"
            {...form.getInputProps('maxBudget')}
          />

          <NumberInput
            label="Rate Limit (requests/minute)"
            placeholder="No limit"
            description="Maximum requests per minute"
            min={1}
            max={10000}
            {...form.getInputProps('rateLimitPerMinute')}
          />

          <Button
            variant="subtle"
            onClick={() => setShowAdvanced(!showAdvanced)}
            size="xs"
          >
            {showAdvanced ? 'Hide' : 'Show'} Advanced Settings
          </Button>

          {showAdvanced && (
            <>
              <TagsInput
                label="Allowed IP Addresses"
                placeholder="Enter IP address and press Enter"
                description="Restrict key usage to specific IP addresses or CIDR ranges"
                {...form.getInputProps('allowedIpAddresses')}
              />

              <Textarea
                label="Metadata (JSON)"
                placeholder='{"department": "engineering", "project": "chatbot"}'
                description="Optional JSON metadata for this key"
                rows={3}
                {...form.getInputProps('metadata')}
              />
            </>
          )}

          <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
            <Text size="sm">
              The API key will be generated and displayed only once after creation. 
              Make sure to copy and store it securely.
            </Text>
          </Alert>

          <Group justify="flex-end" mt="md">
            <Button 
              variant="subtle" 
              onClick={handleClose}
              disabled={createVirtualKey.isPending}
            >
              Cancel
            </Button>
            <Button 
              type="submit" 
              loading={createVirtualKey.isPending}
              disabled={!form.isValid()}
            >
              Create Virtual Key
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
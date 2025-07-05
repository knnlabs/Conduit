'use client';

import {
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
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useCreateVirtualKey } from '@/hooks/api/useAdminApi';
import { useAvailableModels } from '@/hooks/api/useCoreApi';
import { IconInfoCircle } from '@tabler/icons-react';
import { useState } from 'react';
import { FormModal } from '@/components/common/FormModal';
import { validators } from '@/lib/utils/form-validators';

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

  const handleSuccess = () => {
    setShowAdvanced(false);
  };

  const handleClose = () => {
    setShowAdvanced(false);
    onClose();
  };

  // Get available models for selection
  const modelOptions = availableModels?.models?.map((model: unknown) => {
    const m = model as { id: string; provider: string };
    return {
      value: m.id,
      label: `${m.id} (${m.provider})`,
    };
  }) || [];

  // Add option to allow all models
  if (modelOptions.length > 0) {
    modelOptions.unshift({ value: '*', label: 'All Models' });
  }

  // Create a mutation wrapper that handles the payload transformation
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const mutation: any = {
    ...createVirtualKey,
    mutate: (values: CreateVirtualKeyForm, options?: Parameters<typeof createVirtualKey.mutate>[1]) => {
      const payload = {
        keyName: values.keyName.trim(),
        description: values.description?.trim() || undefined,
        maxBudget: values.maxBudget || undefined,
        rateLimitRpm: values.rateLimitPerMinute || undefined,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels.join(',') : undefined,
        metadata: values.metadata?.trim() ? values.metadata : undefined, // SDK expects string not object
      };
      createVirtualKey.mutate(payload, options);
    },
    mutateAsync: async (values: CreateVirtualKeyForm) => {
      const payload = {
        keyName: values.keyName.trim(),
        description: values.description?.trim() || undefined,
        maxBudget: values.maxBudget || undefined,
        rateLimitRpm: values.rateLimitPerMinute || undefined,
        allowedModels: values.allowedModels.length > 0 ? values.allowedModels.join(',') : undefined,
        metadata: values.metadata?.trim() ? values.metadata : undefined, // SDK expects string not object
      };
      return createVirtualKey.mutateAsync(payload);
    },
  };

  return (
    <FormModal
      opened={opened}
      onClose={handleClose}
      title="Create Virtual Key"
      size="lg"
      form={form}
      mutation={mutation}
      entityType="Virtual Key"
      submitText="Create Virtual Key"
      onSuccess={handleSuccess}
    >
      {(form) => (
        <>
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
        </>
      )}
    </FormModal>
  );
}
'use client';

import {
  Modal,
  TextInput,
  Switch,
  Stack,
  Group,
  NumberInput,
  Button,
  Select,
  Divider,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useEffect, useState, useCallback } from 'react';
import { useUpdateModelMapping, useModelMappings } from '@/hooks/useModelMappingsApi';
import { useProviders } from '@/hooks/useProviderApi';
import type { ProviderDto, ModelProviderMappingDto, UpdateModelProviderMappingDto } from '@knn_labs/conduit-admin-client';
import { getProviderTypeFromDto, getProviderDisplayName } from '@/lib/utils/providerTypeUtils';

interface EditModelMappingModalProps {
  isOpen: boolean;
  onClose: () => void;
  mapping: ModelProviderMappingDto | null;
  onSave?: () => void;
}

interface FormValues {
  modelAlias: string;
  modelId?: number;
  providerId: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
  // Provider-specific overrides
  maxContextTokensOverride?: number;
  providerVariation?: string;
  qualityScore?: number;
  // Advanced routing
  isDefault: boolean;
  defaultCapabilityType?: string;
  notes?: string;
}

export function EditModelMappingModal({
  isOpen,
  onClose,
  mapping,
  onSave,
}: EditModelMappingModalProps) {
  const updateMapping = useUpdateModelMapping();
  const { providers } = useProviders();
  const { mappings } = useModelMappings();

  const [initialFormValues, setInitialFormValues] = useState<FormValues>(() => ({
    modelAlias: '',
    modelId: undefined,
    providerId: '',
    providerModelId: '',
    priority: 100,
    isEnabled: true,
    maxContextTokensOverride: undefined,
    providerVariation: undefined,
    qualityScore: undefined,
    isDefault: false,
    defaultCapabilityType: undefined,
    notes: undefined,
  }));

  const form = useForm<FormValues>({
    initialValues: initialFormValues,
    validate: {
      modelAlias: (value) => {
        if (!value?.trim()) return 'Model alias is required';
        
        // Check for duplicates, but exclude the current mapping being edited
        const duplicate = mappings.find(m => 
          m.modelAlias === value && m.id !== (mapping?.id ?? 0)
        );
        
        if (duplicate) {
          return 'Model alias already exists';
        }
        
        return null;
      },
    },
  });

  // Stable callback for form updates
  const updateForm = useCallback((newFormValues: FormValues) => {
    setInitialFormValues(newFormValues);
    form.setValues(newFormValues);
    form.resetDirty();
  }, [form]);

  // Update form when mapping changes
  useEffect(() => {
    if (mapping && providers) {
      
      // The mapping.providerId is now a numeric ID
      const providerIdForForm = mapping.providerId?.toString() ?? '';
      
      
      const newFormValues: FormValues = {
        modelAlias: mapping.modelAlias,
        modelId: mapping.modelId,
        providerId: providerIdForForm, // Use the numeric ID for the form
        providerModelId: mapping.providerModelId,
        priority: mapping.priority ?? 100,
        isEnabled: mapping.isEnabled,
        maxContextTokensOverride: mapping.maxContextTokensOverride,
        providerVariation: mapping.providerVariation,
        qualityScore: mapping.qualityScore,
        isDefault: mapping.isDefault ?? false,
        defaultCapabilityType: mapping.defaultCapabilityType,
        notes: mapping.notes,
      };
      
      updateForm(newFormValues);
    }
  }, [mapping, providers, updateForm]);

  const handleSubmit = async (values: FormValues) => {
    if (!mapping) return;

    const updateData: UpdateModelProviderMappingDto = {
      modelAlias: values.modelAlias,
      modelId: values.modelId,
      providerId: parseInt(values.providerId, 10), // Send numeric ID directly
      providerModelId: values.providerModelId,
      priority: values.priority,
      isEnabled: values.isEnabled,
      maxContextTokensOverride: values.maxContextTokensOverride,
      providerVariation: values.providerVariation,
      qualityScore: values.qualityScore,
      isDefault: values.isDefault,
      defaultCapabilityType: values.defaultCapabilityType,
      notes: values.notes,
    };

    try {
      await updateMapping.mutateAsync({
        id: mapping.id,
        data: updateData,
      });

      onSave?.();
      onClose();
    } catch (error) {
      console.error('[EditModal] Update failed:', error);
    }
  };

  const providerOptions = providers?.map((p: ProviderDto) => {
    try {
      const providerType = getProviderTypeFromDto(p);
      return {
        value: p.id?.toString() ?? '', // Form uses string representation of numeric ID
        label: getProviderDisplayName(providerType),
      };
    } catch {
      return {
        value: p.id?.toString() ?? '',
        label: 'Unknown Provider',
      };
    }
  }).filter(opt => opt.value !== '') || [];

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Edit Model Mapping"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="Model Alias"
            placeholder="e.g., gpt-4-turbo"
            description="The alias used to reference this model in API calls"
            required
            {...form.getInputProps('modelAlias')}
          />

          <NumberInput
            label="Model ID"
            placeholder="Optional"
            description="Reference to the canonical Model entity"
            {...form.getInputProps('modelId')}
          />

          <Select
            label="Provider"
            placeholder="Select provider"
            data={providerOptions}
            {...form.getInputProps('providerId')}
          />

          <TextInput
            label="Provider Model ID"
            placeholder="e.g., gpt-4-turbo"
            description="The model identifier used by the provider"
            {...form.getInputProps('providerModelId')}
          />

          <NumberInput
            label="Priority"
            placeholder="100"
            description="Higher priority mappings are preferred (0-1000)"
            min={0}
            max={1000}
            {...form.getInputProps('priority')}
          />

          <Switch
            label="Enable mapping"
            description="Disabled mappings will not be used for routing"
            {...form.getInputProps('isEnabled', { type: 'checkbox' })}
          />

          <Divider label="Provider Overrides" labelPosition="center" />

          <NumberInput
            label="Max Context Tokens Override"
            placeholder="e.g., 128000"
            description="Override the model's default context window"
            min={0}
            {...form.getInputProps('maxContextTokensOverride')}
          />

          <TextInput
            label="Provider Variation"
            placeholder="e.g., Q4_K_M, GGUF, instruct"
            description="Specific model variation or quantization"
            {...form.getInputProps('providerVariation')}
          />

          <NumberInput
            label="Quality Score"
            placeholder="1.0"
            description="Quality relative to original (0-1)"
            min={0}
            max={1}
            step={0.05}
            {...form.getInputProps('qualityScore')}
          />

          <Divider label="Advanced Settings" labelPosition="center" />

          <Switch
            label="Is Default"
            description="Use as default mapping for its capability"
            {...form.getInputProps('isDefault', { type: 'checkbox' })}
          />

          <TextInput
            label="Default Capability Type"
            placeholder="e.g., chat, vision"
            description="The capability this mapping is default for"
            {...form.getInputProps('defaultCapabilityType')}
          />

          <TextInput
            label="Notes"
            placeholder="Optional notes"
            description="Additional notes about this mapping"
            {...form.getInputProps('notes')}
          />

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button 
              type="submit" 
              loading={updateMapping.isPending}
            >
              Save Changes
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
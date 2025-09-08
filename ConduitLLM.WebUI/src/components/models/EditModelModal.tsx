'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Stack, Group, Textarea, Alert, Text, Tabs, Checkbox, Paper, SimpleGrid, Tooltip, ActionIcon } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle, IconSettings, IconLink, IconTransform } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { ParameterPreview } from '@/components/parameters/ParameterPreview';
import { ProviderTypeList } from './ProviderTypeList';
import { EditProviderTypeModal } from './EditProviderTypeModal';
import { DeleteProviderTypeModal } from './DeleteProviderTypeModal';
import { tryConvertReplicateSchema, isValidReplicateSchema } from '@/utils/replicateSchemaConverter';
import { TOKENIZER_SELECT_OPTIONS, TokenizerType } from '@/lib/utils/tokenizerTypes';
import type { 
  ModelDto, 
  UpdateModelDto, 
  ModelSeriesDto,
  NormalizedProviderTypeAssociation 
} from '@knn_labs/conduit-admin-client';

// Extend ModelDto to include capability fields and modelParameters until SDK types are updated
interface ExtendedModelDto extends ModelDto {
  modelParameters?: string | null;
  supportsChat?: boolean;
  supportsVision?: boolean;
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
  supportsImageGeneration?: boolean;
  supportsVideoGeneration?: boolean;
  supportsEmbeddings?: boolean;
  maxInputTokens?: number | null;
  maxOutputTokens?: number | null;
  tokenizerType?: 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20;
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
  const [jsonError, setJsonError] = useState<string | null>(null);
  
  // Provider type association states
  const [associations, setAssociations] = useState<NormalizedProviderTypeAssociation[]>([]);
  const [loadingAssociations, setLoadingAssociations] = useState(false);
  const [editingAssociation, setEditingAssociation] = useState<NormalizedProviderTypeAssociation | null>(null);
  const [deletingAssociation, setDeletingAssociation] = useState<NormalizedProviderTypeAssociation | null>(null);
  const [showAddAssociation, setShowAddAssociation] = useState(false);
  const [deletingAssociationLoading, setDeletingAssociationLoading] = useState(false);
  
  const { executeWithAdmin } = useAdminClient();

  const form = useForm<{
    name: string;
    modelSeriesId: number | null;
    isActive: boolean;
    modelParameters: string;
    tokenizerType: number;
    supportsChat: boolean;
    supportsVision: boolean;
    supportsFunctionCalling: boolean;
    supportsStreaming: boolean;
    supportsImageGeneration: boolean;
    supportsVideoGeneration: boolean;
    supportsEmbeddings: boolean;
    maxInputTokens: number | null;
    maxOutputTokens: number | null;
  }>({
    initialValues: {
      name: model?.name ?? '',
      modelSeriesId: (model?.modelSeriesId && model.modelSeriesId !== 0) ? model.modelSeriesId : null,
      isActive: model?.isActive ?? true,
      modelParameters: model?.modelParameters ?? '',
      tokenizerType: model?.tokenizerType ?? TokenizerType.Cl100KBase,
      // Capability fields from the model directly (flat structure)
      supportsChat: model?.supportsChat ?? false,
      supportsVision: model?.supportsVision ?? false,
      supportsFunctionCalling: model?.supportsFunctionCalling ?? false,
      supportsStreaming: model?.supportsStreaming ?? false,
      supportsImageGeneration: model?.supportsImageGeneration ?? false,
      supportsVideoGeneration: model?.supportsVideoGeneration ?? false,
      supportsEmbeddings: model?.supportsEmbeddings ?? false,
      maxInputTokens: model?.maxInputTokens ?? null,
      maxOutputTokens: model?.maxOutputTokens ?? null
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null,
      tokenizerType: (value) => {
        if (value === null || value === undefined) return 'Tokenizer type is required';
        if (typeof value !== 'number' || value < 0 || value > Object.keys(TokenizerType).length / 2 - 1) return 'Invalid tokenizer type';
        return null;
      },
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
      },
      maxInputTokens: (value) => {
        if (value !== null && value !== undefined && value < 1024) {
          return 'Minimum value is 1024 tokens';
        }
        return null;
      },
      maxOutputTokens: (value) => {
        if (value !== null && value !== undefined && value < 1024) {
          return 'Minimum value is 1024 tokens';
        }
        return null;
      }
    }
  });

  useEffect(() => {
    if (model) {
      form.setValues({
        name: model.name ?? '',
        modelSeriesId: (model.modelSeriesId && model.modelSeriesId !== 0) ? model.modelSeriesId : null,
        isActive: model.isActive ?? true,
        modelParameters: model.modelParameters ?? '',
        tokenizerType: model.tokenizerType ?? TokenizerType.Cl100KBase,
        // Update capability fields from the model directly (flat structure)
        supportsChat: model.supportsChat ?? false,
        supportsVision: model.supportsVision ?? false,
        supportsFunctionCalling: model.supportsFunctionCalling ?? false,
        supportsStreaming: model.supportsStreaming ?? false,
        supportsImageGeneration: model.supportsImageGeneration ?? false,
        supportsVideoGeneration: model.supportsVideoGeneration ?? false,
        supportsEmbeddings: model.supportsEmbeddings ?? false,
        maxInputTokens: model.maxInputTokens ?? null,
        maxOutputTokens: model.maxOutputTokens ?? null
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [model]);

  useEffect(() => {
    if (isOpen) {
      void loadData();
      if (model?.id) {
        void loadProviderAssociations();
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, model?.id]);

  const loadData = async () => {
    try {
      const seriesData = await executeWithAdmin(client => client.modelSeries.list());
      setSeries(seriesData);
    } catch (error) {
      console.warn('Failed to load data:', error);
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
      const modelId = model.id;
      if (!modelId) throw new Error('Model ID is required');
      
      // Build DTO with only non-null/non-empty values
      // Using Partial<UpdateModelDto> to allow building incrementally
      const dto: Partial<UpdateModelDto> & {
        maxInputTokens?: number | null;
        maxOutputTokens?: number | null;
        modelSeriesId?: number | null;
        tokenizerType?: number;
      } = {
        name: values.name,
        modelSeriesId: (values.modelSeriesId && values.modelSeriesId !== 0) ? values.modelSeriesId : null,
        isActive: values.isActive,
        tokenizerType: values.tokenizerType,
        // Always include boolean capability fields
        supportsChat: values.supportsChat,
        supportsVision: values.supportsVision,
        supportsFunctionCalling: values.supportsFunctionCalling,
        supportsStreaming: values.supportsStreaming,
        supportsImageGeneration: values.supportsImageGeneration,
        supportsVideoGeneration: values.supportsVideoGeneration,
        supportsEmbeddings: values.supportsEmbeddings,
      };
      
      // Only include optional string fields if they have content
      if (values.modelParameters && values.modelParameters.trim() !== '') {
        dto.modelParameters = values.modelParameters;
      }
      
      // Include token fields - null means "clear the value"
      // Backend validates that values must be >= 1024 or null
      dto.maxInputTokens = values.maxInputTokens;
      dto.maxOutputTokens = values.maxOutputTokens;
      
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

  const loadProviderAssociations = async () => {
    if (!model?.id) {
      console.warn('No model ID available for loading provider associations');
      return;
    }
    
    try {
      setLoadingAssociations(true);
      console.warn('Loading associations for model:', model.id);
      
      const modelId = model.id;
      if (!modelId) {
        console.warn('No model ID available');
        return;
      }
      const identifiers = await executeWithAdmin(client => 
        client.models.getIdentifiers(modelId)
      );
      
      console.warn('Loaded associations:', identifiers);
      setAssociations(identifiers);
    } catch (error) {
      console.warn('Failed to load provider associations:', error);
      // Don't show error notification for 404s - just means no associations exist yet
      if (error && typeof error === 'object' && 'status' in error && error.status !== 404) {
        notifications.show({
          title: 'Error',
          message: 'Failed to load provider associations',
          color: 'red',
        });
      }
    } finally {
      setLoadingAssociations(false);
    }
  };



  const handleDeleteAssociation = async () => {
    if (!model?.id || !deletingAssociation?.id) {
      console.warn('Missing model or association ID', { modelId: model?.id, associationId: deletingAssociation?.id });
      return;
    }
    
    try {
      setDeletingAssociationLoading(true);
      console.warn('Deleting association:', { modelId: model.id, associationId: deletingAssociation.id });
      
      const modelId = model.id;
      if (!modelId) {
        console.warn('No model ID available for deletion');
        return;
      }
      await executeWithAdmin(client =>
        client.models.deleteIdentifier(modelId, deletingAssociation.id)
      );
      
      console.warn('Delete successful, reloading associations...');
      
      notifications.show({
        title: 'Success',
        message: 'Provider association deleted successfully',
        color: 'green',
      });
      
      // Reload associations
      await loadProviderAssociations();
      
      console.warn('Associations reloaded');
      
      // Close modal
      setDeletingAssociation(null);
    } catch (error) {
      console.error('Failed to delete provider association:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to delete provider association',
        color: 'red',
      });
    } finally {
      setDeletingAssociationLoading(false);
    }
  };

  const seriesOptions = series
    .filter(s => s.id !== undefined)
    .map(s => ({
      value: s.id?.toString() ?? '',
      label: `${s.name} (${s.authorName})`
    }));

  return (
    <>
      <Modal
        opened={isOpen}
        onClose={handleClose}
        title="Edit Model"
        size="xl"
      >
        <Tabs defaultValue="settings">
          <Tabs.List>
            <Tabs.Tab value="settings" leftSection={<IconSettings size={16} />}>
              Settings
            </Tabs.Tab>
            <Tabs.Tab value="providers" leftSection={<IconLink size={16} />}>
              Provider Associations
            </Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="settings" pt="md">
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
            placeholder="Select a series"
            value={form.values.modelSeriesId && form.values.modelSeriesId !== 0 ? form.values.modelSeriesId.toString() : null}
            onChange={(value) => form.setFieldValue('modelSeriesId', value ? parseInt(value) : null)}
            clearable
            searchable
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

          <Paper p="md" withBorder>
            <Stack gap="sm">
              <Text size="sm" fw={500}>Capabilities</Text>
              
              <SimpleGrid cols={2} spacing="sm">
                <Checkbox
                  label="Text Generation (Chat)"
                  {...form.getInputProps('supportsChat', { type: 'checkbox' })}
                />
                <Checkbox
                  label="Vision"
                  {...form.getInputProps('supportsVision', { type: 'checkbox' })}
                />
                <Checkbox
                  label="Function Calling"
                  {...form.getInputProps('supportsFunctionCalling', { type: 'checkbox' })}
                />
                <Checkbox
                  label="Streaming"
                  {...form.getInputProps('supportsStreaming', { type: 'checkbox' })}
                />
                <Checkbox
                  label="Image Generation"
                  {...form.getInputProps('supportsImageGeneration', { type: 'checkbox' })}
                />
                <Checkbox
                  label="Video Generation"
                  {...form.getInputProps('supportsVideoGeneration', { type: 'checkbox' })}
                />
                <Checkbox
                  label="Embeddings"
                  {...form.getInputProps('supportsEmbeddings', { type: 'checkbox' })}
                />
                <TextInput
                  label="Max Input Tokens"
                  type="number"
                  min={1024}
                  placeholder="e.g., 128000"
                  value={form.values.maxInputTokens?.toString() ?? ''}
                  onChange={(e) => form.setFieldValue('maxInputTokens', e.currentTarget.value ? parseInt(e.currentTarget.value) : null)}
                  error={form.errors.maxInputTokens}
                />
                <TextInput
                  label="Max Output Tokens"
                  type="number"
                  min={1024}
                  placeholder="e.g., 4096"
                  value={form.values.maxOutputTokens?.toString() ?? ''}
                  onChange={(e) => form.setFieldValue('maxOutputTokens', e.currentTarget.value ? parseInt(e.currentTarget.value) : null)}
                  error={form.errors.maxOutputTokens}
                />
              </SimpleGrid>
            </Stack>
          </Paper>






          <Stack gap="xs">
            <Group justify="space-between">
              <div>
                <Text size="sm" fw={500}>
                  Model Parameters (JSON)
                </Text>
                <Text size="xs" c="dimmed">
                  Optional: Override series-level parameters for this specific model
                </Text>
              </div>
              <Tooltip label="Convert Replicate schema to Conduit parameters format">
                <ActionIcon
                  variant="light"
                  color="blue"
                  onClick={() => {
                    const currentValue = form.values.modelParameters;
                    if (!currentValue.trim()) {
                      notifications.show({
                        title: 'No content',
                        message: 'Paste a Replicate schema in the parameters field first',
                        color: 'yellow',
                      });
                      return;
                    }
                    
                    if (isValidReplicateSchema(currentValue)) {
                      const converted = tryConvertReplicateSchema(currentValue);
                      form.setFieldValue('modelParameters', converted);
                      validateJson(converted);
                      notifications.show({
                        title: 'Success',
                        message: 'Replicate schema converted successfully',
                        color: 'green',
                      });
                    } else {
                      notifications.show({
                        title: 'Not a Replicate schema',
                        message: 'The content doesn\'t appear to be a valid Replicate schema',
                        color: 'yellow',
                      });
                    }
                  }}
                >
                  <IconTransform size={16} />
                </ActionIcon>
              </Tooltip>
            </Group>
            
            <ParameterPreview 
              parametersJson={form.values.modelParameters ?? ''}
              context="chat"
              label="Preview UI Components"
              maxHeight={300}
            />
            
            <Textarea
              placeholder="JSON parameters for UI generation (leave empty to use series defaults) or paste Replicate schema and click convert..."
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
                  <Button variant="subtle" onClick={handleClose}>
                    Cancel
                  </Button>
                  <Button type="submit" loading={loading} disabled={!!jsonError}>
                    Update Model
                  </Button>
                </Group>
              </Stack>
            </form>
          </Tabs.Panel>

          <Tabs.Panel value="providers" pt="md">
            <ProviderTypeList
              associations={associations}
              loading={loadingAssociations}
              onAdd={() => setShowAddAssociation(true)}
              onEdit={(association) => setEditingAssociation(association)}
              onDelete={(association) => setDeletingAssociation(association)}
            />
          </Tabs.Panel>
        </Tabs>
      </Modal>

      {/* Add/Edit Provider Type Modal */}
      <EditProviderTypeModal
        isOpen={showAddAssociation || !!editingAssociation}
        modelId={model?.id ?? 0}
        association={editingAssociation ? {
          id: editingAssociation.id,
          identifier: editingAssociation.identifier,
          provider: editingAssociation.provider ?? undefined,
          isPrimary: editingAssociation.isPrimary,
          maxInputTokens: editingAssociation.maxInputTokens,
          maxOutputTokens: editingAssociation.maxOutputTokens,
          speedScore: editingAssociation.speedScore,
          qualityScore: editingAssociation.qualityScore,
          providerVariation: editingAssociation.providerVariation ?? undefined
        } : null}
        onClose={() => {
          setShowAddAssociation(false);
          setEditingAssociation(null);
        }}
        onSave={() => { void loadProviderAssociations(); }}
      />

      {/* Delete Provider Type Modal */}
      <DeleteProviderTypeModal
        isOpen={!!deletingAssociation}
        association={deletingAssociation}
        loading={deletingAssociationLoading}
        onClose={() => setDeletingAssociation(null)}
        onConfirm={() => { void handleDeleteAssociation(); }}
      />
    </>
  );
}
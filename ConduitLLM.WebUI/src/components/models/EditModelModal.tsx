'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Stack, Group, Textarea, Alert, Text, Tabs, Checkbox, Paper, SimpleGrid } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle, IconSettings, IconLink } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { ParameterPreview } from '@/components/parameters/ParameterPreview';
import { ProviderTypeList } from './ProviderTypeList';
import { EditProviderTypeModal } from './EditProviderTypeModal';
import { DeleteProviderTypeModal } from './DeleteProviderTypeModal';
import type { ModelDto, UpdateModelDto, ModelSeriesDto } from '@knn_labs/conduit-admin-client';

// Provider type association interface
interface ProviderTypeAssociation {
  id: number;
  identifier: string;
  provider: string;
  isPrimary: boolean;
}

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
  const [associations, setAssociations] = useState<ProviderTypeAssociation[]>([]);
  const [loadingAssociations, setLoadingAssociations] = useState(false);
  const [editingAssociation, setEditingAssociation] = useState<ProviderTypeAssociation | null>(null);
  const [deletingAssociation, setDeletingAssociation] = useState<ProviderTypeAssociation | null>(null);
  const [showAddAssociation, setShowAddAssociation] = useState(false);
  const [deletingAssociationLoading, setDeletingAssociationLoading] = useState(false);
  
  const { executeWithAdmin } = useAdminClient();

  const form = useForm({
    initialValues: {
      name: model?.name ?? '',
      modelSeriesId: model?.modelSeriesId ?? null,
      isActive: model?.isActive ?? true,
      modelParameters: model?.modelParameters ?? '',
      // Capability fields from the model directly (flat structure)
      supportsChat: model?.supportsChat ?? false,
      supportsVision: model?.supportsVision ?? false,
      supportsFunctionCalling: model?.supportsFunctionCalling ?? false,
      supportsStreaming: model?.supportsStreaming ?? false,
      supportsImageGeneration: model?.supportsImageGeneration ?? false,
      supportsVideoGeneration: model?.supportsVideoGeneration ?? false,
      supportsEmbeddings: model?.supportsEmbeddings ?? false,
      maxInputTokens: model?.maxInputTokens ?? 0,
      maxOutputTokens: model?.maxOutputTokens ?? 0
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
        isActive: model.isActive ?? true,
        modelParameters: model.modelParameters ?? '',
        // Update capability fields from the model directly (flat structure)
        supportsChat: model.supportsChat ?? false,
        supportsVision: model.supportsVision ?? false,
        supportsFunctionCalling: model.supportsFunctionCalling ?? false,
        supportsStreaming: model.supportsStreaming ?? false,
        supportsImageGeneration: model.supportsImageGeneration ?? false,
        supportsVideoGeneration: model.supportsVideoGeneration ?? false,
        supportsEmbeddings: model.supportsEmbeddings ?? false,
        maxInputTokens: model.maxInputTokens ?? 0,
        maxOutputTokens: model.maxOutputTokens ?? 0
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
      const modelId = model.id;
      if (!modelId) throw new Error('Model ID is required');
      
      const dto = {
        name: values.name,
        modelSeriesId: values.modelSeriesId,
        isActive: values.isActive,
        modelParameters: values.modelParameters ?? null,
        // Include capability fields directly in the update
        supportsChat: values.supportsChat,
        supportsVision: values.supportsVision,
        supportsFunctionCalling: values.supportsFunctionCalling,
        supportsStreaming: values.supportsStreaming,
        supportsImageGeneration: values.supportsImageGeneration,
        supportsVideoGeneration: values.supportsVideoGeneration,
        supportsEmbeddings: values.supportsEmbeddings,
        maxInputTokens: values.maxInputTokens,
        maxOutputTokens: values.maxOutputTokens
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
      setAssociations(identifiers as ProviderTypeAssociation[]);
    } catch (error) {
      console.error('Failed to load provider associations:', error);
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
        onClose={onClose}
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
            required
            data={seriesOptions}
            placeholder="Select a series"
            value={form.values.modelSeriesId?.toString() ?? ''}
            onChange={(value) => form.setFieldValue('modelSeriesId', value ? parseInt(value) : null)}
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
                  placeholder="e.g., 128000"
                  {...form.getInputProps('maxInputTokens')}
                  onChange={(e) => form.setFieldValue('maxInputTokens', parseInt(e.currentTarget.value) || 0)}
                />
                <TextInput
                  label="Max Output Tokens"
                  type="number"
                  placeholder="e.g., 4096"
                  {...form.getInputProps('maxOutputTokens')}
                  onChange={(e) => form.setFieldValue('maxOutputTokens', parseInt(e.currentTarget.value) || 0)}
                />
              </SimpleGrid>
            </Stack>
          </Paper>






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
        association={editingAssociation}
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
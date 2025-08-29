'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Stack, Group, Textarea, Alert, Text, Tabs, Badge } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle, IconSettings, IconLink, IconCheck } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { ParameterPreview } from '@/components/parameters/ParameterPreview';
import { ProviderTypeList } from './ProviderTypeList';
import { EditProviderTypeModal } from './EditProviderTypeModal';
import { DeleteProviderTypeModal } from './DeleteProviderTypeModal';
import { getModelCapabilityList } from '@/utils/modelHelpers';
import type { ModelDto, UpdateModelDto, ModelSeriesDto, ModelCapabilitiesDto } from '@knn_labs/conduit-admin-client';

// Provider type association interface
interface ProviderTypeAssociation {
  id: number;
  identifier: string;
  provider: string;
  isPrimary: boolean;
}

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
  
  // Provider type association states
  const [associations, setAssociations] = useState<ProviderTypeAssociation[]>([]);
  const [loadingAssociations, setLoadingAssociations] = useState(false);
  const [editingAssociation, setEditingAssociation] = useState<ProviderTypeAssociation | null>(null);
  const [deletingAssociation, setDeletingAssociation] = useState<ProviderTypeAssociation | null>(null);
  const [showAddAssociation, setShowAddAssociation] = useState(false);
  const [deletingAssociationLoading, setDeletingAssociationLoading] = useState(false);
  
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
      if (model?.id) {
        void loadProviderAssociations();
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, model?.id]);

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

  const loadProviderAssociations = async () => {
    if (!model?.id) {
      console.warn('No model ID available for loading provider associations');
      return;
    }
    
    try {
      setLoadingAssociations(true);
      console.warn('Loading associations for model:', model.id);
      
      const identifiers = await executeWithAdmin(client => 
        client.models.getIdentifiers(model.id!)
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
      
      await executeWithAdmin(client =>
        client.models.deleteIdentifier(model.id!, deletingAssociation.id)
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

  const capabilitiesOptions = [
    { value: '0', label: 'None' },
    ...capabilities
      .filter(c => c.id !== undefined)
      .map(c => {
        const capList = getModelCapabilityList(c);
        const summary = capList.length > 0 ? capList.slice(0, 3).join(' • ') : 'No capabilities';
        return {
          value: c.id?.toString() ?? '',
          label: `Capability Set #${c.id} - ${summary}`
        };
      })
  ];

  // Get the currently selected capability details
  const selectedCapabilityId = form.values.modelCapabilitiesId;
  const selectedCapability = capabilities.find(c => c.id === selectedCapabilityId);

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

          <Stack gap="xs">
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
            
            {selectedCapabilityId && selectedCapabilityId !== 0 && selectedCapability && (
              <Group gap="xs" wrap="wrap">
                {selectedCapability.supportsChat && (
                  <Badge size="sm" color="blue" leftSection={<IconCheck size={12} />}>Text Generation</Badge>
                )}
                {selectedCapability.supportsVision && (
                  <Badge size="sm" color="purple" leftSection={<IconCheck size={12} />}>Vision</Badge>
                )}
                {selectedCapability.supportsFunctionCalling && (
                  <Badge size="sm" color="teal" leftSection={<IconCheck size={12} />}>Function Calling</Badge>
                )}
                {selectedCapability.supportsStreaming && (
                  <Badge size="sm" color="cyan" leftSection={<IconCheck size={12} />}>Streaming</Badge>
                )}
                {selectedCapability.supportsImageGeneration && (
                  <Badge size="sm" color="pink" leftSection={<IconCheck size={12} />}>Image Generation</Badge>
                )}
                {selectedCapability.supportsVideoGeneration && (
                  <Badge size="sm" color="grape" leftSection={<IconCheck size={12} />}>Video Generation</Badge>
                )}
                {selectedCapability.supportsEmbeddings && (
                  <Badge size="sm" color="green" leftSection={<IconCheck size={12} />}>Embeddings</Badge>
                )}
              </Group>
            )}
          </Stack>






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
        onSave={loadProviderAssociations}
      />

      {/* Delete Provider Type Modal */}
      <DeleteProviderTypeModal
        isOpen={!!deletingAssociation}
        association={deletingAssociation}
        loading={deletingAssociationLoading}
        onClose={() => setDeletingAssociation(null)}
        onConfirm={handleDeleteAssociation}
      />
    </>
  );
}
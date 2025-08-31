'use client';

import { useState, useEffect, use } from 'react';
import { useRouter } from 'next/navigation';
import { Container, Title, Paper, TextInput, Select, NumberInput, Switch, Button, Group, Stack, LoadingOverlay, Alert, Textarea } from '@mantine/core';
import { IconAlertCircle } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import type { ModelProviderMappingDto, UpdateModelProviderMappingDto, ProviderDto } from '@knn_labs/conduit-admin-client';

export default function EditModelMappingPage({ params }: { params: Promise<{ id: string }> }) {
  const router = useRouter();
  const resolvedParams = use(params);
  const mappingId = parseInt(resolvedParams.id, 10);
  const isValidId = !isNaN(mappingId);
  
  // Form state
  const [modelAlias, setModelAlias] = useState<string>('');
  const [modelId, setModelId] = useState<number | undefined>();
  const [providerId, setProviderId] = useState<string>('');
  const [providerModelId, setProviderModelId] = useState<string>('');
  const [priority, setPriority] = useState<number>(100);
  const [isEnabled, setIsEnabled] = useState<boolean>(true);
  const [maxContextTokensOverride, setMaxContextTokensOverride] = useState<number | undefined>();
  const [notes, setNotes] = useState<string>('');
  
  // UI state
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [error, setError] = useState<string>('');
  const [providers, setProviders] = useState<ProviderDto[]>([]);
  const [existingMappings, setExistingMappings] = useState<ModelProviderMappingDto[]>([]);
  const [modelAliasError, setModelAliasError] = useState<string>('');

  useEffect(() => {
    if (isValidId) {
      void fetchData();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fetchData = async () => {
    try {
      setIsLoading(true);
      setError('');

      // Fetch the mapping, providers, and existing mappings in parallel
      const mappingData = await withAdminClient(client => 
        client.modelMappings.getById(mappingId)
      );
      
      const providersResponse = await withAdminClient(client => 
        client.providers.list()
      );
      interface ProvidersResponse {
        items: ProviderDto[];
      }
      const providersData = (providersResponse as ProvidersResponse).items;
      
      const mappingsData = await withAdminClient(client => 
        client.modelMappings.list()
      );

      // Set form values
      setModelAlias(mappingData.modelAlias);
      setModelId(mappingData.modelId);
      setProviderModelId(mappingData.providerModelId);
      setPriority(mappingData.priority ?? 100);
      
      // Set provider ID directly if it exists
      if (mappingData.providerId) {
        setProviderId(mappingData.providerId.toString());
      }
      setIsEnabled(mappingData.isEnabled);
      setMaxContextTokensOverride(mappingData.maxContextTokensOverride);
      setNotes(mappingData.notes ?? '');

      setProviders(providersData);
      setExistingMappings(mappingsData);

    } catch (err) {
      console.error('Error fetching data:', err);
      setError('Failed to load model mapping data');
    } finally {
      setIsLoading(false);
    }
  };

  const validateModelAlias = (value: string): boolean => {
    if (!value?.trim()) {
      setModelAliasError('Model alias is required');
      return false;
    }
    
    const duplicate = existingMappings.find(m => 
      m.modelAlias === value && m.id !== mappingId
    );
    
    if (duplicate) {
      setModelAliasError('Model alias already exists');
      return false;
    }
    
    setModelAliasError('');
    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateModelAlias(modelAlias)) return;
    
    if (!providerId) {
      notifications.show({
        title: 'Validation Error',
        message: 'Please select a provider',
        color: 'red',
      });
      return;
    }
    
    try {
      setIsSaving(true);
      
      if (!providerModelId?.trim()) {
        notifications.show({
          title: 'Validation Error',
          message: 'Provider Model ID is required',
          color: 'red',
        });
        setIsSaving(false);
        return;
      }

      const updateData: UpdateModelProviderMappingDto = {
        id: mappingId,
        modelAlias,
        modelId,
        providerId: parseInt(providerId, 10),
        providerModelId,
        priority,
        isEnabled,
        maxContextTokensOverride,
        notes: notes || undefined,
      };

      await withAdminClient(client => 
        client.modelMappings.update(mappingId, updateData)
      );

      notifications.show({
        title: 'Success',
        message: 'Model mapping updated successfully',
        color: 'green',
      });

      router.push('/model-mappings');
    } catch (err) {
      console.error('Error updating mapping:', err);
      notifications.show({
        title: 'Error',
        message: 'Failed to update model mapping',
        color: 'red',
      });
    } finally {
      setIsSaving(false);
    }
  };

  // Handle invalid ID case
  if (!isValidId) {
    return (
      <Container size="md" py="xl">
        <Alert icon={<IconAlertCircle size={16} />} color="red">
          Invalid model mapping ID
        </Alert>
      </Container>
    );
  }

  return (
    <Container size="md" py="xl">
      <Title order={2} mb="lg">Edit Model Mapping</Title>
      
      {error && (
        <Alert icon={<IconAlertCircle size={16} />} color="red" mb="lg">
          {error}
        </Alert>
      )}

      <Paper shadow="xs" p="md" pos="relative">
        <LoadingOverlay visible={isLoading} />
        
        <form onSubmit={(e) => void handleSubmit(e)}>
          <Stack>
            <TextInput
              label="Model Alias"
              description="The alias used by clients to request this model"
              placeholder="e.g., gpt-4, claude-3"
              value={modelAlias}
              onChange={(e) => {
                setModelAlias(e.currentTarget.value);
                validateModelAlias(e.currentTarget.value);
              }}
              error={modelAliasError}
              required
            />

            <NumberInput
              label="Model ID"
              description="Reference to the canonical Model entity (optional)"
              placeholder="e.g., 1"
              value={modelId}
              onChange={(val) => setModelId(val === '' ? undefined : Number(val))}
            />

            <Select
              label="Provider"
              placeholder="Select a provider"
              value={providerId}
              onChange={(value) => setProviderId(value ?? '')}
              data={providers.map(p => ({
                value: p.id?.toString() ?? '',
                label: p.providerName ?? 'Unnamed Provider'
              }))}
              required
            />

            <TextInput
              label="Provider Model ID"
              description="The model ID as known by the provider"
              placeholder="e.g., gpt-4-1106-preview"
              value={providerModelId}
              onChange={(e) => setProviderModelId(e.currentTarget.value)}
              required
            />

            <NumberInput
              label="Priority"
              description="Higher priority mappings are preferred (0-1000)"
              min={0}
              max={1000}
              value={priority}
              onChange={(val) => setPriority(Number(val) || 100)}
            />

            <NumberInput
              label="Max Context Tokens Override"
              description="Override the default context window size (optional)"
              placeholder="e.g., 128000"
              value={maxContextTokensOverride}
              onChange={(val) => setMaxContextTokensOverride(val === '' ? undefined : Number(val))}
            />

            <Textarea
              label="Notes"
              description="Additional notes about this mapping"
              placeholder="Optional notes..."
              value={notes}
              onChange={(e) => setNotes(e.currentTarget.value)}
              rows={3}
            />

            <Switch
              label="Enabled"
              checked={isEnabled}
              onChange={(e) => setIsEnabled(e.currentTarget.checked)}
            />

            <Group justify="flex-end" mt="md">
              <Button variant="subtle" onClick={() => router.push('/model-mappings')}>
                Cancel
              </Button>
              <Button type="submit" loading={isSaving}>
                Save Changes
              </Button>
            </Group>
          </Stack>
        </form>
      </Paper>
    </Container>
  );
}
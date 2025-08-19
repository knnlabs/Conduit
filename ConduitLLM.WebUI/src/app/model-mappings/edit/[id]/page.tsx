'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Container, Title, Paper, TextInput, Select, NumberInput, Switch, Button, Group, Stack, LoadingOverlay, Alert, Textarea } from '@mantine/core';
import { IconAlertCircle } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import type { ModelProviderMappingDto, UpdateModelProviderMappingDto, ProviderCredentialDto } from '@knn_labs/conduit-admin-client';

export default function EditModelMappingPage({ params }: { params: Promise<{ id: string }> }) {
  const router = useRouter();
  const [mappingId, setMappingId] = useState<string>('');
  
  useEffect(() => {
    void params.then(p => setMappingId(p.id));
  }, [params]);
  
  // Form state
  const [modelAlias, setModelAlias] = useState<string>('');
  const [modelId, setModelId] = useState<number | undefined>();
  const [providerId, setProviderId] = useState<string>('');
  const [providerModelId, setProviderModelId] = useState<string>('');
  const [priority, setPriority] = useState<number>(100);
  const [isEnabled, setIsEnabled] = useState<boolean>(true);
  const [isDefault, setIsDefault] = useState<boolean>(false);
  const [maxContextTokensOverride, setMaxContextTokensOverride] = useState<number | undefined>();
  const [providerVariation, setProviderVariation] = useState<string>('');
  const [qualityScore, setQualityScore] = useState<number | undefined>();
  const [defaultCapabilityType, setDefaultCapabilityType] = useState<string>('');
  const [notes, setNotes] = useState<string>('');
  
  // UI state
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [error, setError] = useState<string>('');
  const [providers, setProviders] = useState<ProviderCredentialDto[]>([]);
  const [existingMappings, setExistingMappings] = useState<ModelProviderMappingDto[]>([]);
  const [modelAliasError, setModelAliasError] = useState<string>('');

  useEffect(() => {
    void fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mappingId]);

  const fetchData = async () => {
    try {
      setIsLoading(true);
      setError('');

      // Fetch the mapping, providers, and existing mappings in parallel
      const mappingData = await withAdminClient(client => 
        client.modelMappings.getById(parseInt(mappingId, 10))
      );
      
      const providersResponse = await withAdminClient(client => 
        client.providers.list()
      );
      interface ProvidersResponse {
        items: ProviderCredentialDto[];
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
      setIsDefault(mappingData.isDefault ?? false);
      setMaxContextTokensOverride(mappingData.maxContextTokensOverride);
      setProviderVariation(mappingData.providerVariation ?? '');
      setQualityScore(mappingData.qualityScore);
      setDefaultCapabilityType(mappingData.defaultCapabilityType ?? '');
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
      m.modelAlias === value && m.id !== parseInt(mappingId)
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
        id: parseInt(mappingId, 10),
        modelAlias,
        modelId,
        providerId: parseInt(providerId, 10),
        providerModelId,
        priority,
        isEnabled,
        isDefault,
        maxContextTokensOverride,
        providerVariation: providerVariation || undefined,
        qualityScore,
        defaultCapabilityType: defaultCapabilityType || undefined,
        notes: notes || undefined,
      };

      await withAdminClient(client => 
        client.modelMappings.update(parseInt(mappingId, 10), updateData)
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

            <TextInput
              label="Provider Variation"
              description="Specific model variation (e.g., Q4_K_M, GGUF, instruct)"
              placeholder="Optional"
              value={providerVariation}
              onChange={(e) => setProviderVariation(e.currentTarget.value)}
            />

            <NumberInput
              label="Quality Score"
              description="Model quality relative to original (0.0-1.0)"
              min={0}
              max={1}
              step={0.1}
              decimalScale={2}
              value={qualityScore}
              onChange={(val) => setQualityScore(val === '' ? undefined : Number(val))}
            />

            <TextInput
              label="Default Capability Type"
              description="Default capability for routing (optional)"
              placeholder="e.g., chat, completion"
              value={defaultCapabilityType}
              onChange={(e) => setDefaultCapabilityType(e.currentTarget.value)}
            />

            <Textarea
              label="Notes"
              description="Additional notes about this mapping"
              placeholder="Optional notes..."
              value={notes}
              onChange={(e) => setNotes(e.currentTarget.value)}
              rows={3}
            />

            <Group>
              <Switch
                label="Enabled"
                checked={isEnabled}
                onChange={(e) => setIsEnabled(e.currentTarget.checked)}
              />
              
              <Switch
                label="Is Default"
                checked={isDefault}
                onChange={(e) => setIsDefault(e.currentTarget.checked)}
              />
            </Group>

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
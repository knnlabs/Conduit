'use client';

import { useState, useEffect, use } from 'react';
import { useRouter } from 'next/navigation';
import { 
  Container, 
  Title, 
  Paper, 
  TextInput, 
  Select, 
  NumberInput, 
  Switch, 
  Button, 
  Group, 
  Stack, 
  LoadingOverlay, 
  Alert, 
  Badge,
  Text,
  Card,
  Divider
} from '@mantine/core';
import { IconAlertCircle, IconRobot, IconBolt, IconStar } from '@tabler/icons-react';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import type { 
  ModelProviderMappingDto, 
  UpdateModelProviderMappingDto,
  ConduitAdminClient,
  ModelProviderAvailabilityDto,
} from '@/lib/admin-api';

type AvailableProvider = ModelProviderAvailabilityDto;

interface AssociationDetails extends ModelProviderAvailabilityDto {
  modelId: number;
  modelName: string;
}

export default function EditModelMappingPage({ params }: { params: Promise<{ id: string }> }) {
  const router = useRouter();
  const resolvedParams = use(params);
  const mappingId = parseInt(resolvedParams.id, 10);
  const isValidId = !isNaN(mappingId);
  
  // Form state
  const [modelAlias, setModelAlias] = useState<string>('');
  const [providerId, setProviderId] = useState<string>('');
  const [priority, setPriority] = useState<number>(100);
  const [weight, setWeight] = useState<number>(1);
  const [isEnabled, setIsEnabled] = useState<boolean>(true);
  
  // Data state
  const [currentMapping, setCurrentMapping] = useState<ModelProviderMappingDto | null>(null);
  const [associationDetails, setAssociationDetails] = useState<AssociationDetails | null>(null);
  const [existingMappings, setExistingMappings] = useState<ModelProviderMappingDto[]>([]);
  
  // UI state
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [error, setError] = useState<string>('');
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

      // Fetch the current mapping
      const mappingData = await withAdminClient((client: ConduitAdminClient) => 
        client.modelMappings.getById(mappingId)
      );
      setCurrentMapping(mappingData);
      
      // Fetch all mappings for validation
      const mappingsData = await withAdminClient((client: ConduitAdminClient) => 
        client.modelMappings.list()
      );
      setExistingMappings(mappingsData);

      // Get the model ID from the association
      // First, we need to get all models and find which one has this association
      const models = await withAdminClient((client: ConduitAdminClient) => 
        client.models.list()
      );
      
      // Find the model that contains this association
      let foundAssociation: AssociationDetails | null = null;
      
      for (const model of models) {
        const modelId = model.id;
        if (modelId === undefined) continue;
        
        const availableProviders = await withAdminClient(async (client: ConduitAdminClient) => 
          client.models.getModelProviders(modelId)
        );
        
        const association = availableProviders.find(
          (a: AvailableProvider) => a.associationId === mappingData.modelProviderTypeAssociationId
        );
        
        if (association) {
          foundAssociation = {
            ...association,
            modelId: modelId,
            modelName: model.name ?? 'Unknown Model'
          };
          break;
        }
      }
      
      if (!foundAssociation) {
        setError('Could not find the ModelProviderTypeAssociation for this mapping. The association may have been deleted.');
      } else {
        setAssociationDetails(foundAssociation);
      }

      // Set form values
      setModelAlias(mappingData.modelAlias);
      setProviderId(mappingData.providerId.toString());
      setPriority(mappingData.priority ?? 100);
      setWeight(mappingData.weight ?? 1);
      setIsEnabled(mappingData.isEnabled);

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
      m.modelAlias === value && m.providerId.toString() === providerId && m.id !== mappingId
    );
    
    if (duplicate) {
      setModelAliasError('This alias/provider candidate already exists');
      return false;
    }
    
    setModelAliasError('');
    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateModelAlias(modelAlias)) return;
    
    if (!providerId) {
      notify.error('Please select a provider');
      return;
    }

    if (!currentMapping) {
      notify.error('The current model mapping is unavailable');
      return;
    }
    
    // Validate that the selected provider is valid for this association
    if (associationDetails) {
      const validProvider = associationDetails.availableProviders.find(
        p => p.providerId.toString() === providerId
      );
      
      if (!validProvider) {
        notify.error('The selected provider is not valid for this model association');
        return;
      }
    }
    
    try {
      setIsSaving(true);

      const updateData: UpdateModelProviderMappingDto = {
        modelAlias,
        providerId: parseInt(providerId, 10),
        providerModelId: associationDetails?.identifier ?? currentMapping.providerModelId,
        modelProviderTypeAssociationId: currentMapping.modelProviderTypeAssociationId,
        priority,
        weight,
        isEnabled,
      };

      await withAdminClient((client: ConduitAdminClient) => 
        client.modelMappings.update(mappingId, updateData)
      );

      notify.success('Model mapping updated successfully');

      router.push('/model-mappings');
    } catch (err) {
      console.error('Error updating mapping:', err);
      notify.error('Failed to update model mapping');
    } finally {
      setIsSaving(false);
    }
  };

  const formatTokenLimit = (tokens: number | null) => {
    if (!tokens) return 'Default';
    if (tokens >= 1000000) return `${(tokens / 1000000).toFixed(1)}M`;
    if (tokens >= 1000) return `${(tokens / 1000).toFixed(0)}K`;
    return tokens.toString();
  };

  const formatScore = (score: number | null, type: 'speed' | 'quality') => {
    if (!score) return null;
    
    if (type === 'speed') {
      if (score >= 2) return `${score.toFixed(1)}x faster`;
      if (score === 1) return 'Standard speed';
      return `${(1 / score).toFixed(1)}x slower`;
    }
    
    // Quality score
    const percentage = (score * 100).toFixed(0);
    if (score >= 0.95) return `${percentage}% quality`;
    if (score >= 0.9) return `${percentage}% quality`;
    return `${percentage}% quality (degraded)`;
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
        
        {associationDetails && (
          <Card mb="lg" withBorder>
            <Stack gap="sm">
              <Group justify="space-between">
                <Text fw={600}>Model Configuration</Text>
                <Badge variant="filled">{associationDetails.modelName}</Badge>
              </Group>
              
              <Divider />
              
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Provider Model ID:</Text>
                <Text size="sm" fw={500}>{associationDetails.identifier}</Text>
              </Group>
              
              {associationDetails.provider && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Provider Type:</Text>
                  <Badge variant="outline">{associationDetails.provider}</Badge>
                </Group>
              )}
              
              {associationDetails.providerVariation && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Variation:</Text>
                  <Badge variant="outline">{associationDetails.providerVariation}</Badge>
                </Group>
              )}
              
              <Group gap="xs" mt="xs">
                {associationDetails.maxInputTokens && (
                  <Badge
                    leftSection={<IconRobot size={12} />}
                    variant="light"
                    size="sm"
                  >
                    Input: {formatTokenLimit(associationDetails.maxInputTokens)}
                  </Badge>
                )}
                {associationDetails.maxOutputTokens && (
                  <Badge
                    leftSection={<IconRobot size={12} />}
                    variant="light"
                    size="sm"
                  >
                    Output: {formatTokenLimit(associationDetails.maxOutputTokens)}
                  </Badge>
                )}
                {associationDetails.speedScore && (
                  <Badge
                    leftSection={<IconBolt size={12} />}
                    variant="light"
                    color="green"
                    size="sm"
                  >
                    {formatScore(associationDetails.speedScore, 'speed')}
                  </Badge>
                )}
                {associationDetails.qualityScore && (
                  <Badge
                    leftSection={<IconStar size={12} />}
                    variant="light"
                    color={associationDetails.qualityScore >= 0.9 ? 'blue' : 'orange'}
                    size="sm"
                  >
                    {formatScore(associationDetails.qualityScore, 'quality')}
                  </Badge>
                )}
              </Group>
            </Stack>
          </Card>
        )}
        
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

            <Select
              label="Provider"
              placeholder="Select a provider"
              description={associationDetails ? "Only providers that support this model configuration are shown" : "Loading available providers..."}
              value={providerId}
              onChange={(value) => setProviderId(value ?? '')}
              data={
                associationDetails?.availableProviders.map(p => ({
                  value: p.providerId.toString(),
                  label: `${p.providerName} (${p.providerType})`
                })) ?? []
              }
              disabled={!associationDetails}
              required
            />

            <NumberInput
              label="Priority"
              description="Lower values have higher priority (0-1000)"
              min={0}
              max={1000}
              value={priority}
              onChange={(val) => setPriority(Number(val) || 100)}
            />

            <NumberInput
              label="Balanced score weight"
              description="Multiplier applied after cost, speed, and quality scoring (0.1–2.0)"
              min={0.1}
              max={2}
              step={0.1}
              decimalScale={2}
              value={weight}
              onChange={(val) => setWeight(Number(val) || 1)}
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
              <Button type="submit" loading={isSaving} disabled={!associationDetails}>
                Save Changes
              </Button>
            </Group>
          </Stack>
        </form>
      </Paper>
    </Container>
  );
}

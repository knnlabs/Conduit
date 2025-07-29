'use client';

import { useParams, useRouter } from 'next/navigation';
import { useState, useEffect } from 'react';
import {
  Container,
  Title,
  Paper,
  Stack,
  TextInput,
  Switch,
  Group,
  NumberInput,
  Button,
  Select,
  Divider,
  LoadingOverlay,
  Alert,
  Breadcrumbs,
  Anchor,
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle } from '@tabler/icons-react';
import type { ModelProviderMappingDto, UpdateModelProviderMappingDto, ProviderCredentialDto } from '@knn_labs/conduit-admin-client';
import { getProviderTypeFromDto, getProviderDisplayName } from '@/lib/utils/providerTypeUtils';

export default function EditModelMappingPage() {
  const params = useParams();
  const router = useRouter();
  const mappingId = params?.id as string;

  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [providers, setProviders] = useState<ProviderCredentialDto[]>([]);
  const [existingMappings, setExistingMappings] = useState<ModelProviderMappingDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [modelId, setModelId] = useState('');
  const [providerId, setProviderId] = useState('');
  const [providerModelId, setProviderModelId] = useState('');
  const [priority, setPriority] = useState(100);
  const [isEnabled, setIsEnabled] = useState(true);
  const [supportsVision, setSupportsVision] = useState(false);
  const [supportsImageGeneration, setSupportsImageGeneration] = useState(false);
  const [supportsAudioTranscription, setSupportsAudioTranscription] = useState(false);
  const [supportsTextToSpeech, setSupportsTextToSpeech] = useState(false);
  const [supportsRealtimeAudio, setSupportsRealtimeAudio] = useState(false);
  const [supportsFunctionCalling, setSupportsFunctionCalling] = useState(false);
  const [supportsStreaming, setSupportsStreaming] = useState(false);
  const [supportsVideoGeneration, setSupportsVideoGeneration] = useState(false);
  const [supportsEmbeddings, setSupportsEmbeddings] = useState(false);
  const [supportsChat, setSupportsChat] = useState(false);
  const [maxContextLength, setMaxContextLength] = useState<number | undefined>(undefined);
  const [maxOutputTokens, setMaxOutputTokens] = useState<number | undefined>(undefined);
  const [isDefault, setIsDefault] = useState(false);
  const [modelIdError, setModelIdError] = useState('');

  // Fetch data on mount
  useEffect(() => {
    const fetchData = async () => {
      setIsLoading(true);
      try {
        // Fetch mapping details
        const mappingResponse = await fetch(`/api/model-mappings/${mappingId}`);
        if (!mappingResponse.ok) throw new Error('Failed to fetch mapping');
        const mappingData = await mappingResponse.json() as ModelProviderMappingDto;

        // Fetch providers
        const providersResponse = await fetch('/api/providers');
        if (!providersResponse.ok) throw new Error('Failed to fetch providers');
        const providersData = await providersResponse.json() as ProviderCredentialDto[];
        setProviders(providersData);

        // Fetch all mappings for validation
        const mappingsResponse = await fetch('/api/model-mappings');
        if (!mappingsResponse.ok) throw new Error('Failed to fetch mappings');
        const mappingsData = await mappingsResponse.json() as ModelProviderMappingDto[];
        setExistingMappings(mappingsData);

        // Set form values
        setModelId(mappingData.modelId);
        setProviderModelId(mappingData.providerModelId);
        setPriority(mappingData.priority ?? 100);
        
        // Set provider ID directly if it exists
        if (mappingData.providerId) {
          setProviderId(mappingData.providerId.toString());
        }
        setIsEnabled(mappingData.isEnabled);
        setSupportsVision(mappingData.supportsVision ?? false);
        setSupportsImageGeneration(mappingData.supportsImageGeneration ?? false);
        setSupportsAudioTranscription(mappingData.supportsAudioTranscription ?? false);
        setSupportsTextToSpeech(mappingData.supportsTextToSpeech ?? false);
        setSupportsRealtimeAudio(mappingData.supportsRealtimeAudio ?? false);
        setSupportsFunctionCalling(mappingData.supportsFunctionCalling ?? false);
        setSupportsStreaming(mappingData.supportsStreaming ?? false);
        setSupportsVideoGeneration(mappingData.supportsVideoGeneration ?? false);
        setSupportsEmbeddings(mappingData.supportsEmbeddings ?? false);
        setSupportsChat(mappingData.supportsChat ?? false);
        setMaxContextLength(mappingData.maxContextLength);
        setMaxOutputTokens(mappingData.maxOutputTokens);
        setIsDefault(mappingData.isDefault ?? false);

      } catch (err) {
        console.error('Error fetching data:', err);
        setError('Failed to load model mapping data');
      } finally {
        setIsLoading(false);
      }
    };

    if (mappingId) {
      void fetchData();
    }
  }, [mappingId]);


  const validateModelId = (value: string): boolean => {
    if (!value?.trim()) {
      setModelIdError('Model alias is required');
      return false;
    }
    
    const duplicate = existingMappings.find(m => 
      m.modelId === value && m.id !== parseInt(mappingId)
    );
    
    if (duplicate) {
      setModelIdError('Model alias already exists');
      return false;
    }
    
    setModelIdError('');
    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateModelId(modelId)) return;
    
    if (!providerId) {
      notifications.show({
        title: 'Validation Error',
        message: 'Please select a provider',
        color: 'red',
      });
      return;
    }

    setIsSaving(true);

    try {
      const provider = providers.find(p => p.id.toString() === providerId);
      if (!provider) {
        notifications.show({
          title: 'Error',
          message: 'Selected provider not found',
          color: 'red',
        });
        setIsSaving(false);
        return;
      }
      
      // No longer need to convert provider ID to name

      const updateData: UpdateModelProviderMappingDto = {
        id: parseInt(mappingId, 10), // Backend requires ID in body
        modelId,
        providerId: parseInt(providerId, 10), // Convert string to number
        providerModelId,
        priority,
        isEnabled,
        supportsVision,
        supportsImageGeneration,
        supportsAudioTranscription,
        supportsTextToSpeech,
        supportsRealtimeAudio,
        supportsFunctionCalling,
        supportsStreaming,
        supportsVideoGeneration,
        supportsEmbeddings,
        supportsChat,
        maxContextLength: maxContextLength ?? undefined,
        maxOutputTokens: maxOutputTokens ?? undefined,
        isDefault,
      };

      const response = await fetch(`/api/model-mappings/${mappingId}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(updateData),
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error('[EditPage] Update failed with status:', response.status, 'Error:', errorText);
        throw new Error(`Failed to update model mapping: ${errorText}`);
      }

      notifications.show({
        title: 'Success',
        message: 'Model mapping updated successfully',
        color: 'green',
      });

      // Redirect back to model mappings page
      router.push('/model-mappings');
    } catch (error) {
      console.error('[EditPage] Update failed:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to update model mapping',
        color: 'red',
      });
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    router.push('/model-mappings');
  };

  const providerOptions = providers.map(p => {
    try {
      const providerType = getProviderTypeFromDto(p);
      return {
        value: p.id.toString(),
        label: getProviderDisplayName(providerType),
      };
    } catch {
      return {
        value: p.id.toString(),
        label: 'Unknown Provider',
      };
    }
  });

  if (error) {
    return (
      <Container size="lg" py="xl">
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error" 
          color="red"
        >
          {error}
        </Alert>
      </Container>
    );
  }

  return (
    <Container size="lg" py="xl">
      <Breadcrumbs mb="md">
        <Anchor 
          component="button"
          onClick={() => { router.push('/model-mappings'); }}
          size="sm"
        >
          Model Mappings
        </Anchor>
        <span>Edit</span>
      </Breadcrumbs>

      <Title order={2} mb="lg">Edit Model Mapping</Title>
      
      <Paper shadow="sm" p="lg" pos="relative">
        <LoadingOverlay visible={isLoading} />
        
        <form onSubmit={(e) => { e.preventDefault(); void handleSubmit(e); }}>
          <Stack gap="md">
            <TextInput
              label="Model Alias"
              placeholder="e.g., gpt-4-turbo"
              description="The alias used to reference this model in API calls"
              required
              value={modelId}
              onChange={(e) => {
                setModelId(e.currentTarget.value);
                validateModelId(e.currentTarget.value);
              }}
              error={modelIdError}
            />

            <Select
              label="Provider"
              placeholder="Select provider"
              data={providerOptions}
              value={providerId}
              onChange={(value) => setProviderId(value ?? '')}
              required
              error={!providerId ? 'Provider is required' : undefined}
            />

            <TextInput
              label="Provider Model ID"
              placeholder="e.g., gpt-4-turbo"
              description="The model identifier used by the provider"
              value={providerModelId}
              onChange={(e) => setProviderModelId(e.currentTarget.value)}
            />

            <NumberInput
              label="Priority"
              placeholder="100"
              description="Higher priority mappings are preferred (0-1000)"
              min={0}
              max={1000}
              value={priority}
              onChange={(value) => { setPriority(typeof value === 'number' ? value : 100); }}
            />

            <Switch
              label="Enable mapping"
              description="Disabled mappings will not be used for routing"
              checked={isEnabled}
              onChange={(e) => setIsEnabled(e.currentTarget.checked)}
            />

            <Switch
              label="Default mapping"
              description="Mark this as the default mapping for the model"
              checked={isDefault}
              onChange={(e) => setIsDefault(e.currentTarget.checked)}
            />

            <Divider label="Capabilities" labelPosition="center" />

            <Group grow>
              <Switch
                label="Chat"
                checked={supportsChat}
                onChange={(e) => setSupportsChat(e.currentTarget.checked)}
              />
              <Switch
                label="Vision"
                checked={supportsVision}
                onChange={(e) => setSupportsVision(e.currentTarget.checked)}
              />
            </Group>

            <Group grow>
              <Switch
                label="Streaming"
                checked={supportsStreaming}
                onChange={(e) => setSupportsStreaming(e.currentTarget.checked)}
              />
              <Switch
                label="Function Calling"
                checked={supportsFunctionCalling}
                onChange={(e) => setSupportsFunctionCalling(e.currentTarget.checked)}
              />
            </Group>

            <Group grow>
              <Switch
                label="Image Generation"
                checked={supportsImageGeneration}
                onChange={(e) => setSupportsImageGeneration(e.currentTarget.checked)}
              />
              <Switch
                label="Embeddings"
                checked={supportsEmbeddings}
                onChange={(e) => setSupportsEmbeddings(e.currentTarget.checked)}
              />
            </Group>

            <Group grow>
              <Switch
                label="Audio Transcription"
                checked={supportsAudioTranscription}
                onChange={(e) => setSupportsAudioTranscription(e.currentTarget.checked)}
              />
              <Switch
                label="Text to Speech"
                checked={supportsTextToSpeech}
                onChange={(e) => setSupportsTextToSpeech(e.currentTarget.checked)}
              />
            </Group>

            <Group grow>
              <Switch
                label="Realtime Audio"
                checked={supportsRealtimeAudio}
                onChange={(e) => setSupportsRealtimeAudio(e.currentTarget.checked)}
              />
              <Switch
                label="Video Generation"
                checked={supportsVideoGeneration}
                onChange={(e) => setSupportsVideoGeneration(e.currentTarget.checked)}
              />
            </Group>

            <Divider label="Context Limits" labelPosition="center" />

            <Group grow>
              <NumberInput
                label="Max Context Length"
                placeholder="e.g., 128000"
                description="Maximum input tokens"
                min={0}
                value={maxContextLength}
                onChange={(value) => { setMaxContextLength(typeof value === 'number' ? value : undefined); }}
              />
              <NumberInput
                label="Max Output Tokens"
                placeholder="e.g., 4096"
                description="Maximum output tokens"
                min={0}
                value={maxOutputTokens}
                onChange={(value) => { setMaxOutputTokens(typeof value === 'number' ? value : undefined); }}
              />
            </Group>

            <Group justify="flex-end" mt="xl">
              <Button variant="subtle" onClick={handleCancel}>
                Cancel
              </Button>
              <Button 
                type="submit" 
                loading={isSaving}
              >
                Save Changes
              </Button>
            </Group>
          </Stack>
        </form>
      </Paper>
    </Container>
  );
}
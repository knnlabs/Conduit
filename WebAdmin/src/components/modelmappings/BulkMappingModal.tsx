'use client';

import { useState, useEffect } from 'react';
import {
  Modal,
  Button,
  Select,
  Table,
  Checkbox,
  Group,
  Text,
  Stack,
  Badge,
  Alert,
  NumberInput,
  Switch,
  LoadingOverlay,
  ScrollArea,
  Tooltip,
} from '@mantine/core';
import { notify } from '@/lib/notifications';
import { 
  IconAlertCircle, 
  IconCheck, 
  IconBrain
} from '@tabler/icons-react';
import { useProviders } from '@/hooks/useProviderApi';
import { useBulkDiscoverModels, useBulkCreateMappings } from '@/hooks/useModelMappingsApi';
import { getProviderTypeFromDto, providerTypeToName } from '@/lib/utils/providerTypeUtils';
import { CapabilityIcons } from '@/components/common/CapabilityIcons';
import type { ProviderDto } from '@/lib/admin-api';

interface BulkMappingModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

interface DiscoveredModel {
  modelId: string;
  displayName: string;
  providerId: string;
  providerModelId?: string;
  hasConflict: boolean;
  existingMapping: Record<string, unknown> | null;
  conflictReason: string | null;
  modelProviderTypeAssociationId: number | null;
  capabilities: {
    supportsVision: boolean;
    supportsImageGeneration: boolean;
    supportsAudioTranscription: boolean;
    supportsTextToSpeech: boolean;
    supportsRealtimeAudio: boolean;
    supportsFunctionCalling: boolean;
    supportsStreaming: boolean;
    supportsVideoGeneration: boolean;
    supportsEmbeddings: boolean;
    supportsChat: boolean;
    maxContextLength?: number | null;
    maxOutputTokens?: number | null;
  };
}

export function BulkMappingModal({ isOpen, onClose, onSuccess }: BulkMappingModalProps) {
  const [selectedProviderId, setSelectedProviderId] = useState<string | null>(null);
  const [discoveredModels, setDiscoveredModels] = useState<DiscoveredModel[]>([]);
  const [selectedModels, setSelectedModels] = useState<Set<string>>(new Set());
  const [defaultPriority, setDefaultPriority] = useState(50);
  const [enableByDefault, setEnableByDefault] = useState(true);
  
  const { providers, isLoading: isLoadingProviders } = useProviders();
  const { discoverModels, isDiscovering } = useBulkDiscoverModels();
  const { createMappings, isCreating } = useBulkCreateMappings();
  
  // Reset state when modal closes
  useEffect(() => {
    if (!isOpen) {
      setSelectedProviderId(null);
      setDiscoveredModels([]);
      setSelectedModels(new Set());
    }
  }, [isOpen]);
  
  const handleProviderSelect = async (providerId: string | null) => {
    if (!providerId) return;
    
    setSelectedProviderId(providerId);
    setSelectedModels(new Set());
    
    const provider = providers?.find((p: ProviderDto) => p.id?.toString() === providerId);
    if (!provider) return;
    
    try {
      // Ensure providerType exists
      if (!provider.providerType) {
        throw new Error('Provider must have providerType');
      }
      const providerType = getProviderTypeFromDto(provider as { providerType: number });
      const providerName = providerTypeToName(providerType);
      const result = await discoverModels(providerId, providerName);
      setDiscoveredModels(result.models.map(model => ({
        ...model,
        existingMapping: model.existingMapping as Record<string, unknown> | null
      })));
      
      // Auto-select models without conflicts
      const newSelected = new Set<string>();
      result.models.forEach(model => {
        if (!model.hasConflict) {
          newSelected.add(model.modelId);
        }
      });
      setSelectedModels(newSelected);
      
      if (result.conflictCount > 0) {
        notify.warning(
          `${result.conflictCount} models have conflicts or unresolved associations`,
          'Conflicts Detected'
        );
      }
    } catch {
      // Error handled by hook
      setDiscoveredModels([]);
    }
  };
  
  const handleSelectAll = () => {
    const newSelected = new Set<string>();
    discoveredModels.forEach(model => {
      if (!model.hasConflict) {
        newSelected.add(model.modelId);
      }
    });
    setSelectedModels(newSelected);
  };
  
  const handleSelectNone = () => {
    setSelectedModels(new Set());
  };
  
  const handleToggleModel = (modelId: string) => {
    const newSelected = new Set(selectedModels);
    if (newSelected.has(modelId)) {
      newSelected.delete(modelId);
    } else {
      newSelected.add(modelId);
    }
    setSelectedModels(newSelected);
  };
  
  const handleCreateMappings = async () => {
    const modelsToCreate = discoveredModels.filter(m => selectedModels.has(m.modelId));
    
    if (modelsToCreate.length === 0) {
      notify.error(new Error('Please select at least one model to create mappings'));
      return;
    }
    
    try {
      const result = await createMappings({
        models: modelsToCreate,
        defaultPriority,
        enableByDefault,
      });
      
      if (result.failed > 0) {
        notify.warning(
          `Created ${result.created}, found ${result.existing} existing, and failed ${result.failed} mappings`,
          'Bulk Mapping Complete'
        );
      } else {
        const existingSummary = result.existing > 0 ? `; ${result.existing} already existed` : '';
        notify.success(
          `Successfully created ${result.created} mappings${existingSummary}`,
          'Bulk Mapping Complete'
        );
      }
      
      onSuccess();
    } catch {
      // Error handled by hook
    }
  };
  
  const availableModels = discoveredModels.filter(m => !m.hasConflict);
  const conflictModels = discoveredModels.filter(m => m.hasConflict);
  
  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Bulk Add Model Mappings"
      size="xl"
      closeOnClickOutside={false}
    >
      <Stack>
        <Select
          label="Select Provider"
          placeholder="Choose a provider to discover models"
          data={providers?.map((p: ProviderDto) => ({
            value: p.id?.toString() ?? '',
            label: p.providerName ?? `Provider ${p.id}`,
          })).filter(opt => opt.value !== '' && opt.label !== '') ?? []}
          value={selectedProviderId}
          onChange={(value) => { void handleProviderSelect(value); }}
          disabled={isLoadingProviders}
          leftSection={<IconBrain size={16} />}
        />
        
        {selectedProviderId && discoveredModels.length > 0 && (
          <>
            <Group justify="space-between">
              <Group>
                <NumberInput
                  label="Default Priority"
                  value={defaultPriority}
                  onChange={(value) => setDefaultPriority(Number(value) || 50)}
                  min={0}
                  max={100}
                  style={{ width: 120 }}
                />
                <Switch
                  label="Enable by default"
                  checked={enableByDefault}
                  onChange={(e) => setEnableByDefault(e.currentTarget.checked)}
                  mt="md"
                />
              </Group>
              <Group mt="md">
                <Button size="xs" variant="subtle" onClick={handleSelectAll}>
                  Select All Available
                </Button>
                <Button size="xs" variant="subtle" onClick={handleSelectNone}>
                  Select None
                </Button>
              </Group>
            </Group>
            
            {conflictModels.length > 0 && (
              <Alert icon={<IconAlertCircle />} color="yellow">
                {conflictModels.length} models have conflicts or unresolved associations and will be skipped
              </Alert>
            )}
            
            <ScrollArea h={400} offsetScrollbars>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th style={{ width: 40 }}>Select</Table.Th>
                    <Table.Th>Model ID</Table.Th>
                    <Table.Th>Display Name</Table.Th>
                    <Table.Th>Capabilities</Table.Th>
                    <Table.Th>Context</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {discoveredModels.map(model => (
                    <Table.Tr key={model.modelId}>
                      <Table.Td>
                        <Checkbox
                          checked={selectedModels.has(model.modelId)}
                          onChange={() => handleToggleModel(model.modelId)}
                          disabled={model.hasConflict}
                        />
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" fw={500}>{model.modelId}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{model.displayName}</Text>
                      </Table.Td>
                      <Table.Td>
                        <CapabilityIcons capabilities={model.capabilities} />
                      </Table.Td>
                      <Table.Td>
                        {model.capabilities.maxContextLength && (
                          <Text size="xs" c="dimmed">
                            {model.capabilities.maxContextLength.toLocaleString()}
                          </Text>
                        )}
                      </Table.Td>
                      <Table.Td>
                        {model.hasConflict ? (
                          <Tooltip label={model.conflictReason ?? 'Model cannot be mapped'}>
                            <Badge
                              color={model.existingMapping ? 'yellow' : 'red'}
                              leftSection={<IconAlertCircle size={12} />}
                            >
                              {model.existingMapping ? 'Exists' : 'Unavailable'}
                            </Badge>
                          </Tooltip>
                        ) : (
                          <Badge color="green" leftSection={<IconCheck size={12} />}>
                            Available
                          </Badge>
                        )}
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
            
            <Text size="sm" c="dimmed">
              {selectedModels.size} of {availableModels.length} available models selected
            </Text>
          </>
        )}
        
        <Group justify="flex-end" mt="md">
          <Button variant="subtle" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={() => { void handleCreateMappings(); }}
            disabled={selectedModels.size === 0}
            loading={isCreating}
            leftSection={<IconCheck size={16} />}
          >
            Create {selectedModels.size} Mappings
          </Button>
        </Group>
      </Stack>
      
      <LoadingOverlay visible={isDiscovering} />
    </Modal>
  );
}

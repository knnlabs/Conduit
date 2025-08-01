'use client';

import {
  Modal,
  Stack,
  Group,
  Text,
  Badge,
  Divider,
  Card,
  Code,
} from '@mantine/core';
import { ModelCost } from '../types/modelCost';
import { formatters } from '@/lib/utils/formatters';
import { useEnrichedModelCosts } from '../hooks/useEnrichedModelCosts';

interface ViewModelCostModalProps {
  isOpen: boolean;
  modelCost: ModelCost;
  onClose: () => void;
}

export function ViewModelCostModal({ isOpen, modelCost, onClose }: ViewModelCostModalProps) {
  // Enrich the single model cost with provider information
  const { enrichedCosts } = useEnrichedModelCosts([modelCost]);
  const enrichedCost = enrichedCosts[0] || modelCost;

  const getCostTypeLabel = (type: string) => {
    const labels: Record<string, string> = {
      chat: 'Chat / Completion',
      embedding: 'Embedding',
      image: 'Image Generation',
      audio: 'Audio (Speech/Transcription)',
      video: 'Video Generation',
    };
    return labels[type] || type;
  };

  const hasTokenCosts = modelCost.inputTokenCost !== undefined || 
                       modelCost.outputTokenCost !== undefined;
  
  const hasImageCosts = modelCost.imageCostPerImage !== undefined;
  const hasVideoCosts = modelCost.videoCostPerSecond !== undefined;
  const hasSearchCosts = modelCost.costPerSearchUnit !== undefined;
  const hasInferenceStepCosts = modelCost.costPerInferenceStep !== undefined;
  // Audio costs would be here but backend doesn't support them yet

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Model Pricing Details"
      size="lg"
    >
      <Stack gap="md">
        <Card withBorder>
          <Stack gap="sm">
            <Group justify="space-between">
              <Text fw={600}>Cost Name</Text>
              <Text fw={500}>{modelCost.costName}</Text>
            </Group>
            
            {modelCost.associatedModelAliases && modelCost.associatedModelAliases.length > 0 && (
              <Group justify="space-between" align="flex-start">
                <Text fw={600}>Associated Models</Text>
                <Stack gap={4} align="flex-end">
                  {modelCost.associatedModelAliases.map((alias, index) => (
                    <Code key={index}>{alias}</Code>
                  ))}
                </Stack>
              </Group>
            )}
            
            {'providers' in enrichedCost && enrichedCost.providers.length > 0 && (
              <Group justify="space-between" align="flex-start">
                <Text fw={600}>Providers</Text>
                <Group gap={8}>
                  {enrichedCost.providers.map((provider) => (
                    <Badge key={provider.providerId} variant="light">
                      {provider.providerName}
                    </Badge>
                  ))}
                </Group>
              </Group>
            )}
            
            <Group justify="space-between">
              <Text fw={600}>Model Type</Text>
              <Badge variant="outline" size="lg">{getCostTypeLabel(modelCost.modelType)}</Badge>
            </Group>
            
            <Group justify="space-between">
              <Text fw={600}>Status</Text>
              <Badge 
                color={modelCost.isActive ? 'green' : 'gray'} 
                variant="light"
                size="lg"
              >
                {modelCost.isActive ? 'Active' : 'Inactive'}
              </Badge>
            </Group>
            
            <Group justify="space-between">
              <Text fw={600}>Priority</Text>
              <Text>{modelCost.priority}</Text>
            </Group>
          </Stack>
        </Card>

        {hasTokenCosts && (
          <>
            <Divider label="Token Pricing" labelPosition="center" />
            <Card withBorder>
              <Stack gap="sm">
                {modelCost.inputTokenCost !== undefined && (
                  <Group justify="space-between">
                    <Text>Input Cost</Text>
                    <Group gap="xs">
                      <Text fw={500}>
                        {formatters.currency((modelCost.inputTokenCost ?? 0) * 1000, { currency: 'USD', precision: 4 })}
                      </Text>
                      <Text size="sm" c="dimmed">per 1K tokens</Text>
                    </Group>
                  </Group>
                )}
                
                {modelCost.outputTokenCost !== undefined && (
                  <Group justify="space-between">
                    <Text>Output Cost</Text>
                    <Group gap="xs">
                      <Text fw={500}>
                        {formatters.currency((modelCost.outputTokenCost ?? 0) * 1000, { currency: 'USD', precision: 4 })}
                      </Text>
                      <Text size="sm" c="dimmed">per 1K tokens</Text>
                    </Group>
                  </Group>
                )}
                
                {modelCost.cachedInputTokenCost !== undefined && (
                  <>
                    <Divider variant="dashed" />
                    <Group justify="space-between">
                      <Text>Cached Input Cost</Text>
                      <Group gap="xs">
                        <Text fw={500}>
                          {formatters.currency((modelCost.cachedInputTokenCost ?? 0) * 1000, { currency: 'USD', precision: 4 })}
                        </Text>
                        <Text size="sm" c="dimmed">per 1K tokens</Text>
                      </Group>
                    </Group>
                  </>
                )}
                
                {modelCost.cachedInputWriteCost !== undefined && (
                  <Group justify="space-between">
                    <Text>Cache Write Cost</Text>
                    <Group gap="xs">
                      <Text fw={500}>
                        {formatters.currency((modelCost.cachedInputWriteCost ?? 0) * 1000, { currency: 'USD', precision: 4 })}
                      </Text>
                      <Text size="sm" c="dimmed">per 1K tokens</Text>
                    </Group>
                  </Group>
                )}
              </Stack>
            </Card>
          </>
        )}

        {hasImageCosts && (
          <>
            <Divider label="Image Pricing" labelPosition="center" />
            <Card withBorder>
              <Group justify="space-between">
                <Text>Cost per Image</Text>
                <Text fw={500}>
                  {formatters.currency(modelCost.imageCostPerImage ?? 0, { currency: 'USD' })}
                </Text>
              </Group>
            </Card>
          </>
        )}

        {hasVideoCosts && (
          <>
            <Divider label="Video Pricing" labelPosition="center" />
            <Card withBorder>
              <Group justify="space-between">
                <Text>Cost per Second</Text>
                <Text fw={500}>
                  {formatters.currency(modelCost.videoCostPerSecond ?? 0, { currency: 'USD' })}
                </Text>
              </Group>
            </Card>
          </>
        )}

        {hasSearchCosts && (
          <>
            <Divider label="Search/Rerank Pricing" labelPosition="center" />
            <Card withBorder>
              <Group justify="space-between">
                <Text>Cost per Search Unit</Text>
                <Group gap="xs">
                  <Text fw={500}>
                    {formatters.currency(modelCost.costPerSearchUnit ?? 0, { currency: 'USD', precision: 4 })}
                  </Text>
                  <Text size="sm" c="dimmed">per 1K units</Text>
                </Group>
              </Group>
              <Text size="xs" c="dimmed" mt="xs">
                1 search unit = 1 query + up to 100 documents
              </Text>
            </Card>
          </>
        )}

        {hasInferenceStepCosts && (
          <>
            <Divider label="Inference Step Pricing" labelPosition="center" />
            <Card withBorder>
              <Stack gap="sm">
                <Group justify="space-between">
                  <Text>Cost per Inference Step</Text>
                  <Text fw={500}>
                    {formatters.currency(modelCost.costPerInferenceStep ?? 0, { currency: 'USD', precision: 6 })}
                  </Text>
                </Group>
                {modelCost.defaultInferenceSteps && (
                  <Group justify="space-between">
                    <Text>Default Steps</Text>
                    <Text fw={500}>{modelCost.defaultInferenceSteps}</Text>
                  </Group>
                )}
                {modelCost.defaultInferenceSteps && modelCost.costPerInferenceStep && (
                  <Group justify="space-between">
                    <Text>Default Image Cost</Text>
                    <Text fw={500}>
                      {formatters.currency(
                        modelCost.defaultInferenceSteps * modelCost.costPerInferenceStep,
                        { currency: 'USD', precision: 4 }
                      )}
                    </Text>
                  </Group>
                )}
              </Stack>
              <Text size="xs" c="dimmed" mt="xs">
                Step-based pricing for iterative image generation models
              </Text>
            </Card>
          </>
        )}

        <Divider label="Metadata" labelPosition="center" />
        <Card withBorder>
          <Stack gap="sm">
            <Group justify="space-between">
              <Text>Created</Text>
              <Text size="sm" c="dimmed">
                {formatters.date(modelCost.createdAt)}
              </Text>
            </Group>
            
            <Group justify="space-between">
              <Text>Last Updated</Text>
              <Text size="sm" c="dimmed">
                {formatters.date(modelCost.updatedAt)}
              </Text>
            </Group>
            
            {modelCost.effectiveDate && (
              <Group justify="space-between">
                <Text>Effective Date</Text>
                <Text size="sm" c="dimmed">
                  {formatters.date(modelCost.effectiveDate)}
                </Text>
              </Group>
            )}
            
            {modelCost.expiryDate && (
              <Group justify="space-between">
                <Text>Expiry Date</Text>
                <Text size="sm" c="dimmed">
                  {formatters.date(modelCost.expiryDate)}
                </Text>
              </Group>
            )}
          </Stack>
        </Card>

        {modelCost.description && (
          <Card withBorder bg="gray.0">
            <Stack gap="xs">
              <Text size="sm" fw={600}>Description</Text>
              <Text size="sm">{modelCost.description}</Text>
            </Stack>
          </Card>
        )}
      </Stack>
    </Modal>
  );
}
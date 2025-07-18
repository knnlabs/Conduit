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

interface ViewModelCostModalProps {
  isOpen: boolean;
  modelCost: ModelCost;
  onClose: () => void;
}

export function ViewModelCostModal({ isOpen, modelCost, onClose }: ViewModelCostModalProps) {
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

  const hasTokenCosts = modelCost.inputCostPerMillionTokens !== undefined || 
                       modelCost.outputCostPerMillionTokens !== undefined;
  
  const hasImageCosts = modelCost.costPerImage !== undefined;
  const hasVideoCosts = modelCost.costPerSecond !== undefined;
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
              <Text fw={600}>Model Pattern</Text>
              <Code>{modelCost.modelIdPattern}</Code>
            </Group>
            
            <Group justify="space-between">
              <Text fw={600}>Provider</Text>
              <Badge variant="light" size="lg">{modelCost.providerName}</Badge>
            </Group>
            
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
                {modelCost.inputCostPerMillionTokens !== undefined && (
                  <Group justify="space-between">
                    <Text>Input Cost</Text>
                    <Group gap="xs">
                      <Text fw={500}>
                        {formatters.currency(modelCost.inputCostPerMillionTokens / 1000, { currency: 'USD', precision: 4 })}
                      </Text>
                      <Text size="sm" c="dimmed">per 1K tokens</Text>
                    </Group>
                  </Group>
                )}
                
                {modelCost.outputCostPerMillionTokens !== undefined && (
                  <Group justify="space-between">
                    <Text>Output Cost</Text>
                    <Group gap="xs">
                      <Text fw={500}>
                        {formatters.currency(modelCost.outputCostPerMillionTokens / 1000, { currency: 'USD', precision: 4 })}
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
                  {formatters.currency(modelCost.costPerImage ?? 0, { currency: 'USD' })}
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
                  {formatters.currency(modelCost.costPerSecond ?? 0, { currency: 'USD' })}
                </Text>
              </Group>
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

        <Card withBorder bg="gray.0">
          <Stack gap="xs">
            <Text size="sm" fw={600}>Pattern Matching Example</Text>
            <Text size="sm" c="dimmed">
              This pattern <Code>{modelCost.modelIdPattern}</Code> will match:
            </Text>
            {modelCost.modelIdPattern.endsWith('*') ? (
              <Stack gap={4}>
                <Text size="xs">• {modelCost.modelIdPattern.slice(0, -1)}base</Text>
                <Text size="xs">• {modelCost.modelIdPattern.slice(0, -1)}turbo</Text>
                <Text size="xs">• {modelCost.modelIdPattern.slice(0, -1)}-0125</Text>
                <Text size="xs">• Any model starting with &quot;{modelCost.modelIdPattern.slice(0, -1)}&quot;</Text>
              </Stack>
            ) : (
              <Text size="xs">• Only exact match: {modelCost.modelIdPattern}</Text>
            )}
          </Stack>
        </Card>
      </Stack>
    </Modal>
  );
}
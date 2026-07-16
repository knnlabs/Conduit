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
  SimpleGrid,
  ThemeIcon,
} from '@mantine/core';
import {
  IconMessageCircle,
  IconPhoto,
  IconVideo,
  IconMicrophone,
  IconVectorBezier,
  IconCurrencyDollar,
  IconDatabase,
  IconStairs,
  IconAdjustments,
} from '@tabler/icons-react';
import { ModelCost } from '../types/modelCost';
import { ModelType } from '@knn_labs/conduit-admin-client';
import { formatters } from '@/lib/utils/formatters';
import { useEnrichedModelCosts } from '../hooks/useEnrichedModelCosts';

interface ViewModelCostModalProps {
  isOpen: boolean;
  modelCost: ModelCost;
  onClose: () => void;
}

const MODEL_TYPE_CONFIG: Record<ModelType, { label: string; icon: React.ReactNode; color: string }> = {
  [ModelType.Chat]: {
    label: 'Chat / Completion',
    icon: <IconMessageCircle size={18} />,
    color: 'blue'
  },
  [ModelType.Embedding]: {
    label: 'Embedding',
    icon: <IconVectorBezier size={18} />,
    color: 'violet'
  },
  [ModelType.Image]: {
    label: 'Image Generation',
    icon: <IconPhoto size={18} />,
    color: 'pink'
  },
  [ModelType.Audio]: {
    label: 'Audio (Speech/Transcription)',
    icon: <IconMicrophone size={18} />,
    color: 'cyan'
  },
  [ModelType.Video]: {
    label: 'Video Generation',
    icon: <IconVideo size={18} />,
    color: 'grape'
  },
};

function PricingRow({ label, value, unit }: { label: string; value: number | undefined; unit: string }) {
  if (value === undefined) return null;
  return (
    <Group justify="space-between">
      <Text size="sm">{label}</Text>
      <Group gap="xs">
        <Text fw={500}>{formatters.currency(value, { currency: 'USD', precision: 4 })}</Text>
        <Text size="xs" c="dimmed">{unit}</Text>
      </Group>
    </Group>
  );
}

function ChatPricingSection({ modelCost }: { modelCost: ModelCost }) {
  const hasBasicTokenCosts = modelCost.inputCostPerMillionTokens !== undefined ||
                              modelCost.outputCostPerMillionTokens !== undefined;
  const hasCachedCosts = modelCost.cachedInputCostPerMillionTokens !== undefined ||
                         modelCost.cachedInputWriteCostPerMillionTokens !== undefined;
  const hasSearchCosts = modelCost.costPerSearchUnit !== undefined;

  if (!hasBasicTokenCosts && !hasCachedCosts && !hasSearchCosts) {
    return (
      <Text size="sm" c="dimmed" ta="center" py="md">
        No pricing configured
      </Text>
    );
  }

  return (
    <Stack gap="md">
      {hasBasicTokenCosts && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconCurrencyDollar size={16} />
            <Text size="sm" fw={600}>Token Pricing</Text>
          </Group>
          <Stack gap="xs">
            <PricingRow label="Input" value={modelCost.inputCostPerMillionTokens} unit="per 1M tokens" />
            <PricingRow label="Output" value={modelCost.outputCostPerMillionTokens} unit="per 1M tokens" />
          </Stack>
        </Card>
      )}

      {hasCachedCosts && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconDatabase size={16} />
            <Text size="sm" fw={600}>Prompt Caching</Text>
            <Badge size="xs" variant="light" color="blue">Enabled</Badge>
          </Group>
          <Stack gap="xs">
            <PricingRow label="Cache Read" value={modelCost.cachedInputCostPerMillionTokens} unit="per 1M tokens" />
            <PricingRow label="Cache Write" value={modelCost.cachedInputWriteCostPerMillionTokens} unit="per 1M tokens" />
          </Stack>
        </Card>
      )}

      {hasSearchCosts && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <Text size="sm" fw={600}>Search / Rerank</Text>
          </Group>
          <PricingRow label="Search Unit" value={modelCost.costPerSearchUnit} unit="per 1K units" />
          <Text size="xs" c="dimmed" mt="xs">1 search unit = 1 query + up to 100 documents</Text>
        </Card>
      )}
    </Stack>
  );
}

function EmbeddingPricingSection({ modelCost }: { modelCost: ModelCost }) {
  const hasCosts = modelCost.inputCostPerMillionTokens !== undefined ||
                   modelCost.embeddingCostPerMillionTokens !== undefined;

  if (!hasCosts) {
    return (
      <Text size="sm" c="dimmed" ta="center" py="md">
        No pricing configured
      </Text>
    );
  }

  return (
    <Card withBorder>
      <Group gap="xs" mb="sm">
        <IconCurrencyDollar size={16} />
        <Text size="sm" fw={600}>Embedding Pricing</Text>
      </Group>
      <Stack gap="xs">
        {modelCost.embeddingCostPerMillionTokens !== undefined ? (
          <PricingRow label="Embedding" value={modelCost.embeddingCostPerMillionTokens} unit="per 1M tokens" />
        ) : (
          <PricingRow label="Input" value={modelCost.inputCostPerMillionTokens} unit="per 1M tokens" />
        )}
      </Stack>
    </Card>
  );
}

function ImagePricingSection({ modelCost }: { modelCost: ModelCost }) {
  const hasPerImageCost = modelCost.imageCostPerImage !== undefined;
  const hasStepCost = modelCost.costPerInferenceStep !== undefined;
  const hasMultipliers = modelCost.imageQualityMultipliers ?? modelCost.imageResolutionMultipliers;

  if (!hasPerImageCost && !hasStepCost) {
    return (
      <Text size="sm" c="dimmed" ta="center" py="md">
        No pricing configured
      </Text>
    );
  }

  // Parse multipliers if they exist
  let qualityMultipliers: Record<string, number> | null = null;
  let resolutionMultipliers: Record<string, number> | null = null;

  try {
    if (modelCost.imageQualityMultipliers && modelCost.imageQualityMultipliers !== '{}') {
      qualityMultipliers = JSON.parse(modelCost.imageQualityMultipliers) as Record<string, number>;
    }
    if (modelCost.imageResolutionMultipliers && modelCost.imageResolutionMultipliers !== '{}') {
      resolutionMultipliers = JSON.parse(modelCost.imageResolutionMultipliers) as Record<string, number>;
    }
  } catch (error) {
    // Multipliers stay null; surface that the stored JSON is corrupt
    console.warn('Failed to parse image multipliers for model cost:', error);
  }

  return (
    <Stack gap="md">
      {hasPerImageCost && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconCurrencyDollar size={16} />
            <Text size="sm" fw={600}>Per Image Pricing</Text>
          </Group>
          <PricingRow label="Base Cost" value={modelCost.imageCostPerImage} unit="per image" />
        </Card>
      )}

      {hasStepCost && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconStairs size={16} />
            <Text size="sm" fw={600}>Step-Based Pricing</Text>
          </Group>
          <Stack gap="xs">
            <PricingRow label="Per Step" value={modelCost.costPerInferenceStep} unit="per step" />
            {modelCost.defaultInferenceSteps && (
              <Group justify="space-between">
                <Text size="sm">Default Steps</Text>
                <Text fw={500}>{modelCost.defaultInferenceSteps}</Text>
              </Group>
            )}
            {modelCost.defaultInferenceSteps && modelCost.costPerInferenceStep && (
              <Group justify="space-between">
                <Text size="sm">Typical Image Cost</Text>
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
            For diffusion models with configurable inference steps
          </Text>
        </Card>
      )}

      {hasMultipliers && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconAdjustments size={16} />
            <Text size="sm" fw={600}>Pricing Multipliers</Text>
          </Group>
          <SimpleGrid cols={2} spacing="md">
            {qualityMultipliers && Object.keys(qualityMultipliers).length > 0 && (
              <Stack gap="xs">
                <Text size="xs" c="dimmed">Quality</Text>
                {Object.entries(qualityMultipliers).map(([quality, multiplier]) => (
                  <Group key={quality} justify="space-between">
                    <Text size="sm" tt="capitalize">{quality}</Text>
                    <Badge size="sm" variant="light">{multiplier}x</Badge>
                  </Group>
                ))}
              </Stack>
            )}
            {resolutionMultipliers && Object.keys(resolutionMultipliers).length > 0 && (
              <Stack gap="xs">
                <Text size="xs" c="dimmed">Resolution</Text>
                {Object.entries(resolutionMultipliers).map(([resolution, multiplier]) => (
                  <Group key={resolution} justify="space-between">
                    <Text size="sm">{resolution}</Text>
                    <Badge size="sm" variant="light">{multiplier}x</Badge>
                  </Group>
                ))}
              </Stack>
            )}
          </SimpleGrid>
        </Card>
      )}
    </Stack>
  );
}

function AudioPricingSection({ modelCost }: { modelCost: ModelCost }) {
  const hasMinuteCost = modelCost.audioCostPerMinute !== undefined;
  const hasCharacterCost = modelCost.audioCostPerKCharacters !== undefined;
  const hasInputOutputCosts = modelCost.audioInputCostPerMinute !== undefined ||
                              modelCost.audioOutputCostPerMinute !== undefined;

  if (!hasMinuteCost && !hasCharacterCost && !hasInputOutputCosts) {
    return (
      <Text size="sm" c="dimmed" ta="center" py="md">
        No pricing configured
      </Text>
    );
  }

  return (
    <Stack gap="md">
      {hasMinuteCost && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconCurrencyDollar size={16} />
            <Text size="sm" fw={600}>Per Minute Pricing</Text>
          </Group>
          <PricingRow label="Audio" value={modelCost.audioCostPerMinute} unit="per minute" />
        </Card>
      )}

      {hasInputOutputCosts && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconCurrencyDollar size={16} />
            <Text size="sm" fw={600}>Input/Output Pricing</Text>
          </Group>
          <Stack gap="xs">
            <PricingRow label="Input (STT)" value={modelCost.audioInputCostPerMinute} unit="per minute" />
            <PricingRow label="Output (TTS)" value={modelCost.audioOutputCostPerMinute} unit="per minute" />
          </Stack>
        </Card>
      )}

      {hasCharacterCost && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconCurrencyDollar size={16} />
            <Text size="sm" fw={600}>Character-Based Pricing</Text>
            <Badge size="xs" variant="light">TTS</Badge>
          </Group>
          <PricingRow label="Characters" value={modelCost.audioCostPerKCharacters} unit="per 1K chars" />
        </Card>
      )}
    </Stack>
  );
}

function VideoPricingSection({ modelCost }: { modelCost: ModelCost }) {
  const hasVideoCost = modelCost.videoCostPerSecond !== undefined;

  // Parse resolution multipliers if they exist
  let resolutionMultipliers: Record<string, number> | null = null;
  try {
    if (modelCost.videoResolutionMultipliers && modelCost.videoResolutionMultipliers !== '{}') {
      resolutionMultipliers = JSON.parse(modelCost.videoResolutionMultipliers) as Record<string, number>;
    }
  } catch (error) {
    // Multipliers stay null; surface that the stored JSON is corrupt
    console.warn('Failed to parse video resolution multipliers for model cost:', error);
  }

  if (!hasVideoCost) {
    return (
      <Text size="sm" c="dimmed" ta="center" py="md">
        No pricing configured
      </Text>
    );
  }

  return (
    <Stack gap="md">
      <Card withBorder>
        <Group gap="xs" mb="sm">
          <IconCurrencyDollar size={16} />
          <Text size="sm" fw={600}>Per Second Pricing</Text>
        </Group>
        <PricingRow label="Video" value={modelCost.videoCostPerSecond} unit="per second" />
      </Card>

      {resolutionMultipliers && Object.keys(resolutionMultipliers).length > 0 && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconAdjustments size={16} />
            <Text size="sm" fw={600}>Resolution Multipliers</Text>
          </Group>
          <Stack gap="xs">
            {Object.entries(resolutionMultipliers).map(([resolution, multiplier]) => (
              <Group key={resolution} justify="space-between">
                <Text size="sm">{resolution}</Text>
                <Badge size="sm" variant="light">{multiplier}x</Badge>
              </Group>
            ))}
          </Stack>
        </Card>
      )}
    </Stack>
  );
}

export function ViewModelCostModal({ isOpen, modelCost, onClose }: ViewModelCostModalProps) {
  // Enrich the single model cost with provider information
  const { enrichedCosts } = useEnrichedModelCosts([modelCost]);
  const enrichedCost = enrichedCosts[0] ?? modelCost;

  const typeConfig = MODEL_TYPE_CONFIG[modelCost.modelType] ?? {
    label: modelCost.modelType,
    icon: <IconCurrencyDollar size={18} />,
    color: 'gray'
  };

  const renderPricingSection = () => {
    switch (modelCost.modelType) {
      case ModelType.Chat:
        return <ChatPricingSection modelCost={modelCost} />;
      case ModelType.Embedding:
        return <EmbeddingPricingSection modelCost={modelCost} />;
      case ModelType.Image:
        return <ImagePricingSection modelCost={modelCost} />;
      case ModelType.Audio:
        return <AudioPricingSection modelCost={modelCost} />;
      case ModelType.Video:
        return <VideoPricingSection modelCost={modelCost} />;
      default:
        return <ChatPricingSection modelCost={modelCost} />;
    }
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Model Pricing Details"
      size="lg"
    >
      <Stack gap="md">
        {/* Header Card */}
        <Card withBorder>
          <Stack gap="sm">
            <Group justify="space-between" align="flex-start">
              <Stack gap={4}>
                <Text fw={600} size="lg">{modelCost.costName}</Text>
                <Group gap="xs">
                  <ThemeIcon size="sm" variant="light" color={typeConfig.color}>
                    {typeConfig.icon}
                  </ThemeIcon>
                  <Badge variant="outline" color={typeConfig.color}>
                    {typeConfig.label}
                  </Badge>
                </Group>
              </Stack>
              <Badge
                color={modelCost.isActive ? 'green' : 'gray'}
                variant="light"
                size="lg"
              >
                {modelCost.isActive ? 'Active' : 'Inactive'}
              </Badge>
            </Group>

            {modelCost.associatedModelAliases && modelCost.associatedModelAliases.length > 0 && (
              <>
                <Divider />
                <Stack gap={4}>
                  <Text size="xs" c="dimmed">Associated Models</Text>
                  <Group gap={4}>
                    {modelCost.associatedModelAliases.map((alias, index) => (
                      <Code key={index}>{alias}</Code>
                    ))}
                  </Group>
                </Stack>
              </>
            )}

            {'providers' in enrichedCost && enrichedCost.providers.length > 0 && (
              <>
                <Divider />
                <Stack gap={4}>
                  <Text size="xs" c="dimmed">Providers</Text>
                  <Group gap={8}>
                    {enrichedCost.providers.map((provider) => (
                      <Badge key={provider.providerId} variant="light" size="sm">
                        {provider.providerName}
                      </Badge>
                    ))}
                  </Group>
                </Stack>
              </>
            )}
          </Stack>
        </Card>

        {/* Pricing Section - Type-specific */}
        <Divider label="Pricing" labelPosition="center" />
        {renderPricingSection()}

        {/* Batch Processing */}
        {modelCost.supportsBatchProcessing && (
          <>
            <Divider label="Batch Processing" labelPosition="center" />
            <Card withBorder>
              <Group justify="space-between">
                <Text size="sm">Batch Discount</Text>
                <Badge color="green" size="lg">
                  {modelCost.batchProcessingMultiplier
                    ? `${((1 - modelCost.batchProcessingMultiplier) * 100).toFixed(0)}% off`
                    : 'Enabled'}
                </Badge>
              </Group>
            </Card>
          </>
        )}

        {/* Metadata */}
        <Divider label="Details" labelPosition="center" />
        <Card withBorder>
          <SimpleGrid cols={2} spacing="xs">
            <Stack gap={2}>
              <Text size="xs" c="dimmed">Priority</Text>
              <Text size="sm" fw={500}>{modelCost.priority}</Text>
            </Stack>
            <Stack gap={2}>
              <Text size="xs" c="dimmed">Last Updated</Text>
              <Text size="sm">{formatters.date(modelCost.updatedAt)}</Text>
            </Stack>
            {modelCost.effectiveDate && (
              <Stack gap={2}>
                <Text size="xs" c="dimmed">Effective Date</Text>
                <Text size="sm">{formatters.date(modelCost.effectiveDate)}</Text>
              </Stack>
            )}
            {modelCost.expiryDate && (
              <Stack gap={2}>
                <Text size="xs" c="dimmed">Expiry Date</Text>
                <Text size="sm">{formatters.date(modelCost.expiryDate)}</Text>
              </Stack>
            )}
          </SimpleGrid>
        </Card>

        {modelCost.description && (
          <Card withBorder>
            <Stack gap="xs">
              <Text size="xs" c="dimmed">Description</Text>
              <Text size="sm">{modelCost.description}</Text>
            </Stack>
          </Card>
        )}
      </Stack>
    </Modal>
  );
}

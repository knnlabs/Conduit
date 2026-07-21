'use client';

import { useEffect, useState } from 'react';
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
  Center,
  Loader,
  Alert,
} from '@mantine/core';
import {
  IconMessageCircle,
  IconPhoto,
  IconVideo,
  IconMicrophone,
  IconVectorBezier,
  IconCurrencyDollar,
  IconDatabase,
  IconAdjustments,
  IconInfoCircle,
  IconShieldCheck,
} from '@tabler/icons-react';
import { PricingModel, type ModelDto, type ModelCostDto } from '@/lib/admin-api';
import { formatters } from '@/lib/utils/formatters';
import { useAdminClient } from '@/lib/client/adminClient';
import { extractCapabilities } from '@/utils/typeGuards';

interface ModelCostPreviewModalProps {
  isOpen: boolean;
  model: ModelDto;
  onClose: () => void;
}

const MODEL_TYPE_CONFIG: Record<string, { label: string; icon: React.ReactNode; color: string }> = {
  chat: {
    label: 'Chat / Completion',
    icon: <IconMessageCircle size={18} />,
    color: 'blue'
  },
  embedding: {
    label: 'Embedding',
    icon: <IconVectorBezier size={18} />,
    color: 'violet'
  },
  image: {
    label: 'Image Generation',
    icon: <IconPhoto size={18} />,
    color: 'pink'
  },
  audio: {
    label: 'Audio (Speech/Transcription)',
    icon: <IconMicrophone size={18} />,
    color: 'cyan'
  },
  video: {
    label: 'Video Generation',
    icon: <IconVideo size={18} />,
    color: 'grape'
  },
};

function PricingRow({ label, value, unit }: { label: string; value: number | undefined | null; unit: string }) {
  if (value === undefined || value === null) return null;
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

function TokenPricingSection({ modelCost }: { modelCost: ModelCostDto }) {
  const hasBasicTokenCosts = modelCost.inputCostPerMillionTokens !== undefined ||
                              modelCost.outputCostPerMillionTokens !== undefined;
  const hasCachedCosts = modelCost.cachedInputCostPerMillionTokens !== undefined ||
                         modelCost.cachedInputWriteCostPerMillionTokens !== undefined;

  if (!hasBasicTokenCosts && !hasCachedCosts) {
    return null;
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
    </Stack>
  );
}

// Image and video pricing sections removed - now handled via RulesBased pricing configuration
// Use PricingModel.PerImage, PricingModel.PerVideo, or PricingModel.PerSecondVideo instead

interface PricingRuleConfig {
  conditions: Record<string, unknown>;
  rate: number;
  priority?: number;
  description?: string;
}

interface PricingConstraints {
  minDuration?: number;
  maxDuration?: number;
  minSteps?: number;
  maxSteps?: number;
  allowedResolutions?: string[];
}

interface PricingConfiguration {
  version?: string;
  pricingType?: string;
  unitField?: string;
  defaultRate?: number;
  rules?: PricingRuleConfig[];
  constraints?: PricingConstraints;
}

function RulesBasedPricingSection({ modelCost }: { modelCost: ModelCostDto }) {
  if (!modelCost.pricingConfiguration) {
    return null;
  }

  let config: PricingConfiguration | null = null;

  try {
    config = JSON.parse(modelCost.pricingConfiguration) as PricingConfiguration;
  } catch {
    return (
      <Alert color="red" variant="light">
        <Text size="sm">Invalid pricing configuration JSON</Text>
      </Alert>
    );
  }

  if (!config) return null;

  return (
    <Stack gap="md">
      <Card withBorder>
        <Group gap="xs" mb="sm">
          <IconAdjustments size={16} />
          <Text size="sm" fw={600}>Rules-Based Pricing</Text>
          <Badge size="xs" variant="light" color="blue">
            {config.pricingType ?? 'per_unit'}
          </Badge>
        </Group>

        <Stack gap="sm">
          {config.unitField && (
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Unit Field</Text>
              <Code>{config.unitField}</Code>
            </Group>
          )}

          {config.defaultRate !== undefined && (
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Default Rate</Text>
              <Text fw={500}>{formatters.currency(config.defaultRate, { currency: 'USD', precision: 6 })}</Text>
            </Group>
          )}
        </Stack>
      </Card>

      {config.rules && config.rules.length > 0 && (
        <Card withBorder>
          <Text size="sm" fw={600} mb="sm">Pricing Rules ({config.rules.length})</Text>
          <Stack gap="xs">
            {config.rules.map((rule: PricingRuleConfig, index: number) => (
              <Card key={index} withBorder p="sm" bg="var(--mantine-color-default-hover)">
                <Group justify="space-between" mb="xs">
                  <Text size="sm" fw={500}>
                    {rule.description ?? `Rule ${index + 1}`}
                  </Text>
                  <Group gap="xs">
                    {rule.priority !== undefined && (
                      <Badge size="xs" variant="outline">Priority: {rule.priority}</Badge>
                    )}
                    <Badge size="sm" color="green">
                      {formatters.currency(rule.rate, { currency: 'USD', precision: 6 })}
                    </Badge>
                  </Group>
                </Group>
                <Group gap={4}>
                  {Object.entries(rule.conditions).map(([key, value]) => (
                    <Badge key={key} size="xs" variant="light" color="gray">
                      {key}: {String(value)}
                    </Badge>
                  ))}
                </Group>
              </Card>
            ))}
          </Stack>
        </Card>
      )}

      {/* Validation Constraints */}
      {config.constraints && (
        <Card withBorder>
          <Group gap="xs" mb="sm">
            <IconShieldCheck size={16} />
            <Text size="sm" fw={600}>Validation Constraints</Text>
          </Group>
          <Stack gap="xs">
            {(config.constraints.minDuration !== undefined || config.constraints.maxDuration !== undefined) && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Duration</Text>
                <Text size="sm">
                  {config.constraints.minDuration ?? 0}s - {config.constraints.maxDuration ?? '∞'}s
                </Text>
              </Group>
            )}
            {(config.constraints.minSteps !== undefined || config.constraints.maxSteps !== undefined) && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Steps</Text>
                <Text size="sm">
                  {config.constraints.minSteps ?? 1} - {config.constraints.maxSteps ?? '∞'}
                </Text>
              </Group>
            )}
            {config.constraints.allowedResolutions && config.constraints.allowedResolutions.length > 0 && (
              <Group justify="space-between" align="flex-start">
                <Text size="sm" c="dimmed">Resolutions</Text>
                <Group gap={4}>
                  {config.constraints.allowedResolutions.map((res) => (
                    <Badge key={res} size="xs" variant="light">{res}</Badge>
                  ))}
                </Group>
              </Group>
            )}
          </Stack>
        </Card>
      )}
    </Stack>
  );
}

function getModelTypeFromCapabilities(model: ModelDto): string {
  const capabilities = extractCapabilities(model);

  if (capabilities.supportsVideoGeneration) return 'video';
  if (capabilities.supportsImageGeneration) return 'image';
  if (capabilities.supportsEmbeddings) return 'embedding';
  // Default to chat for text models
  return 'chat';
}

export function ModelCostPreviewModal({ isOpen, model, onClose }: ModelCostPreviewModalProps) {
  const [loading, setLoading] = useState(false);
  const [modelCost, setModelCost] = useState<ModelCostDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { executeWithAdmin } = useAdminClient();

  useEffect(() => {
    if (!isOpen || !model.name) {
      setModelCost(null);
      setError(null);
      return;
    }

    const fetchModelCost = async () => {
      setLoading(true);
      setError(null);

      try {
        // Fetch all model costs and find the one associated with this model
        const result = await executeWithAdmin(client =>
          client.modelCosts.list({ pageSize: 200 })
        );

        // Debug: log the response structure
        console.warn('Model costs API response:', result);

        // Safely access items - handle both array and paginated response formats
        const items: ModelCostDto[] = Array.isArray(result)
          ? result as ModelCostDto[]
          : (result?.items ?? []);

        // Find a cost that has this model's name in its associated aliases
        // The alias format is typically "provider/model-name", so we check both:
        // 1. Exact match
        // 2. Alias ends with the model name (e.g., "minimax/hailuo-2.3" matches "hailuo-2.3")
        const modelNameLower = model.name?.toLowerCase() ?? '';
        const matchingCost = items.find((cost: ModelCostDto) =>
          cost.associatedModelAliases?.some((alias: string) => {
            const aliasLower = alias.toLowerCase();
            return aliasLower === modelNameLower ||
                   aliasLower.endsWith(`/${modelNameLower}`);
          })
        );

        setModelCost(matchingCost ?? null);
      } catch (err) {
        console.error('Failed to fetch model cost:', err);
        const errorMessage = err instanceof Error ? err.message : 'Unknown error';
        setError(`Failed to load pricing information: ${errorMessage}`);
      } finally {
        setLoading(false);
      }
    };

    void fetchModelCost();
  }, [isOpen, model.name, executeWithAdmin]);

  const inferredModelType = getModelTypeFromCapabilities(model);
  const typeConfig = MODEL_TYPE_CONFIG[modelCost?.modelType ?? inferredModelType] ?? {
    label: modelCost?.modelType ?? inferredModelType,
    icon: <IconCurrencyDollar size={18} />,
    color: 'gray'
  };

  const renderPricingContent = () => {
    if (loading) {
      return (
        <Center py="xl">
          <Loader size="md" />
        </Center>
      );
    }

    if (error) {
      return (
        <Alert color="red" variant="light" icon={<IconInfoCircle size={16} />}>
          {error}
        </Alert>
      );
    }

    if (!modelCost) {
      return (
        <Alert color="yellow" variant="light" icon={<IconInfoCircle size={16} />}>
          <Stack gap="xs">
            <Text size="sm" fw={500}>No pricing configured</Text>
            <Text size="xs" c="dimmed">
              This model does not have an associated pricing configuration.
              You can add one in the Model Costs section.
            </Text>
          </Stack>
        </Alert>
      );
    }

    // Check if this is rules-based pricing
    const isRulesBased = modelCost.pricingModel === PricingModel.RulesBased;

    return (
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
                  {isRulesBased && (
                    <Badge variant="light" color="blue">
                      Rules-Based
                    </Badge>
                  )}
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
          </Stack>
        </Card>

        {/* Pricing Section - based on model type and pricing model */}
        <Divider label="Pricing" labelPosition="center" />

        {isRulesBased ? (
          <RulesBasedPricingSection modelCost={modelCost} />
        ) : (
          <>
            {/* Token-based pricing for chat, embedding, and standard models */}
            <TokenPricingSection modelCost={modelCost} />

            {/* Note for image/video models using standard pricing */}
            {(inferredModelType === 'image' || inferredModelType === 'video') && (
              <Alert variant="light" color="blue" icon={<IconInfoCircle size={16} />}>
                <Text size="sm">
                  Image and video models use Rules-Based pricing for detailed cost configuration.
                  Edit this cost to switch to Rules-Based pricing model.
                </Text>
              </Alert>
            )}
          </>
        )}

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
    );
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={
        <Group gap="xs">
          <IconCurrencyDollar size={20} />
          <Text fw={600}>Pricing for {model.name ?? 'Model'}</Text>
        </Group>
      }
      size="lg"
    >
      {renderPricingContent()}
    </Modal>
  );
}

'use client';

import {
  Stack,
  Card,
  Text,
  Badge,
  Group,
  Radio,
  Paper,
  Alert,
} from '@mantine/core';
import { IconRobot, IconBolt, IconStar, IconServer } from '@tabler/icons-react';
import type { AssociationWithProvider } from '@/hooks/useModelAssociations';

interface AssociationProviderSelectProps {
  associations: AssociationWithProvider[];
  value: string | null; // Format: "associationId:providerId"
  onChange: (value: string) => void;
  disabled?: boolean;
}

export function AssociationProviderSelect({
  associations,
  value,
  onChange,
  disabled,
}: AssociationProviderSelectProps) {
  if (associations.length === 0) {
    return (
      <Alert color="yellow">
        No provider configurations available for this model. 
        An administrator must first configure ModelProviderTypeAssociations and Providers.
      </Alert>
    );
  }

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

  return (
    <Stack gap="md">
      <Radio.Group value={value ?? ''} onChange={onChange}>
        <Stack gap="sm">
          {associations.map((assoc) => (
            <Paper key={assoc.associationId} p="sm" withBorder>
              <Stack gap="xs">
                <Group justify="space-between">
                  <Text fw={600}>{assoc.identifier}</Text>
                  {assoc.providerVariation && (
                    <Badge variant="outline" size="sm">
                      {assoc.providerVariation}
                    </Badge>
                  )}
                </Group>

                <Group gap="xs">
                  {assoc.maxInputTokens && (
                    <Badge
                      leftSection={<IconRobot size={12} />}
                      variant="light"
                      size="sm"
                    >
                      Input: {formatTokenLimit(assoc.maxInputTokens)}
                    </Badge>
                  )}
                  {assoc.maxOutputTokens && (
                    <Badge
                      leftSection={<IconRobot size={12} />}
                      variant="light"
                      size="sm"
                    >
                      Output: {formatTokenLimit(assoc.maxOutputTokens)}
                    </Badge>
                  )}
                  {assoc.speedScore && (
                    <Badge
                      leftSection={<IconBolt size={12} />}
                      variant="light"
                      color="green"
                      size="sm"
                    >
                      {formatScore(assoc.speedScore, 'speed')}
                    </Badge>
                  )}
                  {assoc.qualityScore && (
                    <Badge
                      leftSection={<IconStar size={12} />}
                      variant="light"
                      color={assoc.qualityScore >= 0.9 ? 'blue' : 'orange'}
                      size="sm"
                    >
                      {formatScore(assoc.qualityScore, 'quality')}
                    </Badge>
                  )}
                </Group>

                <Text size="sm" fw={500} mt="xs">
                  Available Providers:
                </Text>
                
                <Stack gap="xs" ml="md">
                  {assoc.availableProviders.map((provider) => (
                    <Card
                      key={provider.providerId}
                      p="xs"
                      withBorder
                      style={{
                        borderColor: value === `${assoc.associationId}:${provider.providerId}` 
                          ? 'var(--mantine-color-blue-6)' 
                          : undefined,
                        backgroundColor: value === `${assoc.associationId}:${provider.providerId}`
                          ? 'var(--mantine-color-blue-0)'
                          : undefined,
                      }}
                    >
                      <Radio
                        value={`${assoc.associationId}:${provider.providerId}`}
                        label={
                          <Group gap="xs">
                            <IconServer size={16} />
                            <Text size="sm">
                              {provider.providerName} ({provider.providerType})
                            </Text>
                          </Group>
                        }
                        disabled={disabled}
                      />
                    </Card>
                  ))}
                </Stack>
              </Stack>
            </Paper>
          ))}
        </Stack>
      </Radio.Group>
    </Stack>
  );
}
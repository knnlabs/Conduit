'use client';

import { Stack, Text, Button, Group, Card, Alert, ThemeIcon } from '@mantine/core';
import { IconAlertCircle, IconPlus, IconSettings } from '@tabler/icons-react';
import { useRouter } from 'next/navigation';

interface CallToActionEmptyProps {
  hasProviders: boolean;
  hasModelMappings: boolean;
}

export function CallToActionEmpty({ hasProviders, hasModelMappings }: CallToActionEmptyProps) {
  const router = useRouter();

  if (hasProviders && hasModelMappings) {
    return null;
  }

  const getTitle = () => {
    if (!hasProviders && !hasModelMappings) {
      return 'Setup Required: Add Providers and Model Mappings';
    }
    if (!hasProviders) {
      return 'Setup Required: Add Providers';
    }
    return 'Setup Required: Add Model Mappings';
  };

  const getDescription = () => {
    if (!hasProviders && !hasModelMappings) {
      return 'Before configuring model pricing, you need to set up providers and create model mappings. This ensures your models are properly connected and ready for cost tracking.';
    }
    if (!hasProviders) {
      return 'You need to add at least one provider before configuring model pricing. Providers connect Conduit to LLM services like OpenAI, Anthropic, etc.';
    }
    return 'You need to create model mappings before configuring pricing. Model mappings define which models are available and how they map to your providers.';
  };

  const getSteps = () => {
    if (!hasProviders && !hasModelMappings) {
      return [
        { number: 1, text: 'Add one or more LLM providers (OpenAI, Anthropic, etc.)', action: 'Add Provider', route: '/llm-providers' },
        { number: 2, text: 'Create model mappings to define available models', action: 'Add Model Mappings', route: '/model-mappings' },
        { number: 3, text: 'Return here to configure pricing for your models', action: null, route: null }
      ];
    }
    if (!hasProviders) {
      return [
        { number: 1, text: 'Add one or more LLM providers (OpenAI, Anthropic, etc.)', action: 'Add Provider', route: '/llm-providers' },
        { number: 2, text: 'Return here to configure pricing for your models', action: null, route: null }
      ];
    }
    return [
      { number: 1, text: 'Create model mappings to define available models', action: 'Add Model Mappings', route: '/model-mappings' },
      { number: 2, text: 'Return here to configure pricing for your models', action: null, route: null }
    ];
  };

  return (
    <Card>
      <Alert 
        icon={<IconAlertCircle size={16} />} 
        title={getTitle()}
        color="blue"
        variant="light"
      >
        <Stack gap="md" mt="sm">
          <Text size="sm">
            {getDescription()}
          </Text>
          
          <Stack gap="xs">
            {getSteps().map((step) => (
              <Group key={step.number} gap="sm" align="flex-start">
                <ThemeIcon 
                  size="sm" 
                  variant="light" 
                  color="blue"
                  style={{ minWidth: 24, marginTop: 2 }}
                >
                  {step.number}
                </ThemeIcon>
                <Stack gap={4} style={{ flex: 1 }}>
                  <Text size="sm">{step.text}</Text>
                  {step.action && step.route && (
                    <div>
                      <Button
                        size="xs"
                        variant="light"
                        leftSection={step.route.includes('provider') ? <IconSettings size={14} /> : <IconPlus size={14} />}
                        onClick={() => router.push(step.route)}
                      >
                        {step.action}
                      </Button>
                    </div>
                  )}
                </Stack>
              </Group>
            ))}
          </Stack>
        </Stack>
      </Alert>
    </Card>
  );
}
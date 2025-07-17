'use client';

import { useState, useEffect, useMemo } from 'react';
import { Card, Text, Stack, Badge, Group, LoadingOverlay, ScrollArea } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';

interface PatternPreviewProps {
  pattern: string;
}

interface ProviderModel {
  id: string;
  modelId: string;
  providerId: string;
  providerModelId: string;
  displayName?: string;
}

// Common model patterns for demonstration
const COMMON_MODELS = [
  // OpenAI
  { modelId: 'openai/gpt-4o', provider: 'openai' },
  { modelId: 'openai/gpt-4o-mini', provider: 'openai' },
  { modelId: 'openai/gpt-4-turbo', provider: 'openai' },
  { modelId: 'openai/gpt-4', provider: 'openai' },
  { modelId: 'openai/gpt-3.5-turbo', provider: 'openai' },
  { modelId: 'openai/dall-e-3', provider: 'openai' },
  { modelId: 'openai/whisper-1', provider: 'openai' },
  { modelId: 'openai/text-embedding-3-small', provider: 'openai' },
  { modelId: 'openai/text-embedding-3-large', provider: 'openai' },
  
  // Anthropic
  { modelId: 'anthropic/claude-3-opus', provider: 'anthropic' },
  { modelId: 'anthropic/claude-3-sonnet', provider: 'anthropic' },
  { modelId: 'anthropic/claude-3-haiku', provider: 'anthropic' },
  { modelId: 'anthropic/claude-3.5-sonnet', provider: 'anthropic' },
  { modelId: 'anthropic/claude-3.5-haiku', provider: 'anthropic' },
  
  // Google
  { modelId: 'google/gemini-1.5-pro', provider: 'google' },
  { modelId: 'google/gemini-1.5-flash', provider: 'google' },
  { modelId: 'google/gemini-1.0-pro', provider: 'google' },
  
  // MiniMax
  { modelId: 'minimax/abab6.5g', provider: 'minimax' },
  { modelId: 'minimax/abab6.5s', provider: 'minimax' },
  { modelId: 'minimax/abab5.5', provider: 'minimax' },
  
  // Replicate
  { modelId: 'replicate/meta/llama-2-70b-chat', provider: 'replicate' },
  { modelId: 'replicate/stability-ai/sdxl', provider: 'replicate' },
  { modelId: 'replicate/openai/whisper', provider: 'replicate' },
];

export function PatternPreview({ pattern }: PatternPreviewProps) {
  const [debouncedPattern, setDebouncedPattern] = useState(pattern);

  // Debounce pattern changes to avoid too many API calls
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedPattern(pattern);
    }, 300);
    return () => clearTimeout(timer);
  }, [pattern]);

  // Fetch available models from API (optional - can use static list for now)
  const { data: providerModels, isLoading } = useQuery({
    queryKey: ['provider-models'],
    queryFn: async () => {
      try {
        const response = await fetch('/api/model-mappings');
        if (!response.ok) return [];
        const data = await response.json();
        return data.items || [];
      } catch {
        return [];
      }
    },
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  });

  // Combine provider models with common models
  const allModels = useMemo(() => {
    const modelMap = new Map<string, { modelId: string; provider: string }>();
    
    // Add common models
    COMMON_MODELS.forEach(model => {
      modelMap.set(model.modelId, model);
    });
    
    // Add provider models if available
    if (providerModels && Array.isArray(providerModels)) {
      providerModels.forEach((mapping: any) => {
        if (mapping.modelId && mapping.providerId) {
          modelMap.set(mapping.modelId, {
            modelId: mapping.modelId,
            provider: mapping.providerId,
          });
        }
      });
    }
    
    return Array.from(modelMap.values());
  }, [providerModels]);

  // Match models against pattern
  const matchingModels = useMemo(() => {
    if (!debouncedPattern) return [];
    
    const trimmedPattern = debouncedPattern.trim();
    if (!trimmedPattern) return [];
    
    return allModels.filter(model => {
      if (trimmedPattern.endsWith('*')) {
        const prefix = trimmedPattern.slice(0, -1);
        return model.modelId.startsWith(prefix);
      }
      return model.modelId === trimmedPattern;
    });
  }, [debouncedPattern, allModels]);

  // Group by provider
  const modelsByProvider = useMemo(() => {
    const grouped = new Map<string, string[]>();
    matchingModels.forEach(model => {
      const models = grouped.get(model.provider) || [];
      models.push(model.modelId);
      grouped.set(model.provider, models);
    });
    return grouped;
  }, [matchingModels]);

  if (!pattern.trim()) {
    return null;
  }

  return (
    <Card withBorder>
      <Stack gap="sm">
        <Group gap="xs">
          <IconInfoCircle size={16} />
          <Text fw={600} size="sm">Pattern Preview</Text>
        </Group>
        
        <LoadingOverlay visible={isLoading} />
        
        {matchingModels.length === 0 ? (
          <Text size="sm" c="dimmed">
            No models match this pattern yet. The pattern will still work for future models.
          </Text>
        ) : (
          <>
            <Text size="sm" c="dimmed">
              {matchingModels.length} model{matchingModels.length !== 1 ? 's' : ''} will use this pricing:
            </Text>
            
            <ScrollArea h={150}>
              <Stack gap="xs">
                {Array.from(modelsByProvider.entries()).map(([provider, models]) => (
                  <div key={provider}>
                    <Badge variant="light" size="xs" mb={4}>
                      {provider}
                    </Badge>
                    <Stack gap={2} ml="md">
                      {models.map(modelId => (
                        <Text key={modelId} size="xs" c="dimmed">
                          • {modelId}
                        </Text>
                      ))}
                    </Stack>
                  </div>
                ))}
              </Stack>
            </ScrollArea>
          </>
        )}
        
        {pattern.endsWith('*') && (
          <Text size="xs" c="dimmed" fs="italic">
            This pattern will also match any future models starting with &quot;{pattern.slice(0, -1)}&quot;
          </Text>
        )}
      </Stack>
    </Card>
  );
}
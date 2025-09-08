'use client';

import { useState, useCallback } from 'react';
import { Textarea, Button, Group, Text, Stack } from '@mantine/core';
import { IconVideo } from '@tabler/icons-react';
import { useVideoStore } from '../hooks/useVideoStore';
import { useEnhancedVideoGeneration } from '../hooks/useEnhancedVideoGeneration';
import type { DiscoveryModel } from '@/app/chat/hooks/useDiscoveryModels';

interface VideoPromptInputProps {
  models: DiscoveryModel[];
  dynamicParameters?: Record<string, unknown>;
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
export default function EnhancedVideoPromptInput({ models, dynamicParameters }: VideoPromptInputProps) {
  const [prompt, setPrompt] = useState('');
  const { settings, currentTask, setError } = useVideoStore();
  
  // Use enhanced hook with fallback to polling
  const { generateVideo, isGenerating } = useEnhancedVideoGeneration({
    fallbackToPolling: true,
  });

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!prompt.trim()) {
      setError('Please enter a prompt');
      return;
    }

    if (!settings.model) {
      setError('Please select a model');
      return;
    }

    setError(null);
    
    try {
      await generateVideo({
        prompt: prompt.trim(),
        settings,
        dynamicParameters,
      });
      // Clear prompt after successful submission
      setPrompt('');
    } catch (error) {
      // Error is handled in the hook
      console.error('Error in VideoPromptInput:', error);
    }
  }, [prompt, settings, generateVideo, setError, dynamicParameters]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      void handleSubmit(e as React.FormEvent);
    }
  };

  const isDisabled = isGenerating || !!(currentTask && (currentTask.status === 'pending' || currentTask.status === 'running'));

  return (
    <form onSubmit={(e) => { e.preventDefault(); void handleSubmit(e); }}>
      <Stack gap="md">
        <Textarea
          label="Video Prompt"
          placeholder="Describe the video you want to generate..."
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          onKeyDown={(e) => void handleKeyDown(e)}
          disabled={isDisabled}
          minRows={4}
          autosize
          maxRows={10}
          description="Press Enter to generate, Shift+Enter for new line"
        />
        
        <Group justify="space-between">
          <Group gap="md">
            <Text size="sm" c="dimmed">
              {prompt.length} characters
            </Text>
            {currentTask && (
              <Text size="sm" c="blue" fw={500}>
                Video generation in progress...
              </Text>
            )}
          </Group>
          
          <Button
            type="submit"
            disabled={isDisabled || !prompt.trim()}
            leftSection={<IconVideo size={16} />}
            loading={isGenerating}
          >
            {isGenerating ? 'Generating...' : 'Generate Video'}
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
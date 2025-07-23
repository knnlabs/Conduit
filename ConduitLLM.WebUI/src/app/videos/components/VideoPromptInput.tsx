'use client';

import { useState, useCallback } from 'react';
import { Paper, Textarea, Group, Text, Button } from '@mantine/core';
import { IconVideo } from '@tabler/icons-react';
import { useVideoStore } from '../hooks/useVideoStore';
import { useVideoGeneration } from '../hooks/useVideoGeneration';
import type { VideoModel } from '../types';

interface VideoPromptInputProps {
  models: VideoModel[];
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
export default function VideoPromptInput({ models }: VideoPromptInputProps) {
  const [prompt, setPrompt] = useState('');
  const { settings, currentTask, setError } = useVideoStore();
  const { generateVideo, isGenerating } = useVideoGeneration();

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
      });
      // Clear prompt after successful submission
      setPrompt('');
    } catch (error) {
      // Error is handled in the hook
      console.error('Error in VideoPromptInput:', error);
    }
  }, [prompt, settings, generateVideo, setError]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      void handleSubmit(e as React.FormEvent);
    }
  };

  const isDisabled = isGenerating || !!currentTask;

  return (
    <Paper p="md" withBorder>
      <form onSubmit={(e) => { e.preventDefault(); void handleSubmit(e); }}>
        <Textarea
          label="Video Prompt"
          placeholder="Describe the video you want to generate..."
          value={prompt}
          onChange={(e) => setPrompt(e.currentTarget.value)}
          onKeyDown={(e) => void handleKeyDown(e)}
          disabled={isDisabled}
          minRows={4}
          autosize
          maxRows={10}
        />
        
        <Group justify="space-between" mt="md">
          <div>
            <Text size="sm" c="dimmed">{prompt.length} characters</Text>
            {currentTask && (
              <Text size="sm" c="blue">
                Video generation in progress...
              </Text>
            )}
          </div>
          
          <Button
            type="submit"
            leftSection={<IconVideo size={16} />}
            loading={isGenerating}
            disabled={isDisabled || !prompt.trim()}
          >
            Generate Video
          </Button>
        </Group>
      </form>
    </Paper>
  );
}
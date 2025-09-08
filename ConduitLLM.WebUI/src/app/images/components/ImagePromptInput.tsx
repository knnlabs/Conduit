'use client';

import { useState, useCallback } from 'react';
import { Button, Group } from '@mantine/core';
import { IconPalette, IconTrash } from '@tabler/icons-react';
import { useImageStore } from '../hooks/useImageStore';
import { MediaPromptInput } from '@/app/components/media';

interface ImagePromptInputProps {
  dynamicParameters?: Record<string, unknown>;
}

export default function ImagePromptInput({ dynamicParameters }: ImagePromptInputProps) {
  const { 
    prompt, 
    status, 
    setPrompt, 
    generateImages, 
    clearResults 
  } = useImageStore();

  const [localPrompt, setLocalPrompt] = useState(prompt);

  const handlePromptChange = useCallback((value: string) => {
    setLocalPrompt(value);
    setPrompt(value);
  }, [setPrompt]);

  const handleGenerate = useCallback(() => {
    if (!localPrompt.trim()) {
      return;
    }
    void generateImages(dynamicParameters);
  }, [localPrompt, generateImages, dynamicParameters]);

  const handleClear = useCallback(() => {
    clearResults();
  }, [clearResults]);

  const isGenerating = status === 'generating';

  return (
    <>
      <MediaPromptInput
        value={localPrompt}
        onChange={handlePromptChange}
        onSubmit={handleGenerate}
        label="Image Prompt"
        placeholder="Describe the image you want to generate..."
        disabled={isGenerating}
        isLoading={isGenerating}
        submitShortcut="ctrl+cmd+enter"
        showCharCount={true}
      />
      
      <Group justify="flex-end" mt="md">
        {status === 'completed' && (
          <Button
            onClick={handleClear}
            variant="subtle"
            leftSection={<IconTrash size={16} />}
            disabled={isGenerating}
          >
            Clear Results
          </Button>
        )}
        
        <Button
          onClick={() => void handleGenerate()}
          disabled={!localPrompt.trim() || isGenerating}
          leftSection={<IconPalette size={16} />}
          loading={isGenerating}
        >
          Generate Images
        </Button>
      </Group>
    </>
  );
}
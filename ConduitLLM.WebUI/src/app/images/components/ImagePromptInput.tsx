'use client';

import { useState } from 'react';
import { Textarea, Button, Group, Text } from '@mantine/core';
import { IconPalette, IconTrash } from '@tabler/icons-react';
import { useImageStore } from '../hooks/useImageStore';

export default function ImagePromptInput() {
  const { 
    prompt, 
    status, 
    setPrompt, 
    generateImages, 
    clearResults 
  } = useImageStore();

  const [localPrompt, setLocalPrompt] = useState(prompt);

  const handlePromptChange = (value: string) => {
    setLocalPrompt(value);
    setPrompt(value);
  };

  const handleGenerate = async () => {
    if (!localPrompt.trim()) {
      return;
    }
    void generateImages();
  };

  const handleClear = () => {
    clearResults();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      void handleGenerate();
    }
  };

  const isGenerating = status === 'generating';

  return (
    <>
      <Textarea
        label="Image Prompt"
        value={localPrompt}
        onChange={(e) => handlePromptChange(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Describe the image you want to generate... (Ctrl+Enter to generate)"
        disabled={isGenerating}
        minRows={4}
        autosize
        maxRows={10}
      />
      
      <Group justify="space-between" mt="md">
        <Text size="sm" c="dimmed">
          {localPrompt.length > 0 && `${localPrompt.length} characters`}
          {localPrompt.length > 1000 && ' (very long prompt)'}
        </Text>
        
        <Group>
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
      </Group>
    </>
  );
}
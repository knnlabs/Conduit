'use client';

import { useState, useEffect } from 'react';
import { Textarea, Button, Group, Stack } from '@mantine/core';
import { IconCheck, IconX } from '@tabler/icons-react';
import type { ChatMessage } from '@/types/chat';
import type { TextContent } from '@/app/chat/types';

interface MessageEditorProps {
  message: ChatMessage;
  onSave: (messageId: string, newContent: string) => void;
  onCancel: () => void;
}

export function MessageEditor({ message, onSave, onCancel }: MessageEditorProps) {
  let initialContent = '';
  
  if (typeof message.content === 'string') {
    initialContent = message.content;
  } else if (Array.isArray(message.content)) {
    const textContent = message.content.find((c): c is TextContent => {
      return 'type' in c && c.type === 'text';
    });
    initialContent = textContent?.text ?? '';
  }
    
  const [content, setContent] = useState(initialContent);

  useEffect(() => {
    setContent(initialContent);
  }, [initialContent]);

  const handleSave = () => {
    if (content.trim() && content !== initialContent && message.id) {
      onSave(message.id, content);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      handleSave();
    } else if (e.key === 'Escape') {
      onCancel();
    }
  };

  return (
    <Stack gap="xs">
      <Textarea
        value={content}
        onChange={(e) => setContent(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Edit message..."
        autosize
        minRows={2}
        maxRows={20}
        autoFocus
        styles={{
          input: {
            fontFamily: 'inherit',
            fontSize: 'inherit',
          }
        }}
      />
      <Group gap="xs">
        <Button
          size="xs"
          leftSection={<IconCheck size={16} />}
          onClick={handleSave}
          disabled={!content.trim() || content === initialContent}
        >
          Save
        </Button>
        <Button
          size="xs"
          variant="subtle"
          leftSection={<IconX size={16} />}
          onClick={onCancel}
        >
          Cancel
        </Button>
        <span style={{ fontSize: '0.75rem', color: 'var(--mantine-color-dimmed)' }}>
          Ctrl+Enter to save, Esc to cancel
        </span>
      </Group>
    </Stack>
  );
}
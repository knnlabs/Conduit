'use client';

import { Button, Menu, Switch, Stack, Text } from '@mantine/core';
import { IconDownload, IconFileText, IconMarkdown, IconJson } from '@tabler/icons-react';
import { useState } from 'react';
import type { Conversation } from '@/types/chat';

interface ConversationExportProps {
  conversation: Conversation;
  onExport: (format: string, options: ExportOptions) => void;
}

export interface ExportOptions {
  includeMetadata: boolean;
  includeSystemMessages: boolean;
}

export function ConversationExport({ conversation, onExport }: ConversationExportProps) {
  const [includeMetadata, setIncludeMetadata] = useState(false);
  const [includeSystemMessages, setIncludeSystemMessages] = useState(false);

  const handleExport = (format: string) => {
    onExport(format, {
      includeMetadata,
      includeSystemMessages,
    });
  };

  return (
    <Menu shadow="md" width={280}>
      <Menu.Target>
        <Button
          variant="subtle"
          leftSection={<IconDownload size={16} />}
          size="sm"
        >
          Export
        </Button>
      </Menu.Target>

      <Menu.Dropdown>
        <Stack gap="xs" p="xs">
          <Text size="sm" fw={500}>Export Options</Text>
          
          <Switch
            label="Include metadata"
            size="xs"
            checked={includeMetadata}
            onChange={(e) => setIncludeMetadata(e.currentTarget.checked)}
          />
          
          <Switch
            label="Include system messages"
            size="xs"
            checked={includeSystemMessages}
            onChange={(e) => setIncludeSystemMessages(e.currentTarget.checked)}
          />
        </Stack>

        <Menu.Divider />

        <Menu.Item
          leftSection={<IconJson size={14} />}
          onClick={() => handleExport('json')}
        >
          Export as JSON
        </Menu.Item>

        <Menu.Item
          leftSection={<IconMarkdown size={14} />}
          onClick={() => handleExport('markdown')}
        >
          Export as Markdown
        </Menu.Item>

        <Menu.Item
          leftSection={<IconFileText size={14} />}
          onClick={() => handleExport('txt')}
        >
          Export as Text
        </Menu.Item>

        <Menu.Item
          leftSection={<IconFileText size={14} />}
          onClick={() => handleExport('pdf')}
        >
          Export as PDF
        </Menu.Item>
      </Menu.Dropdown>
    </Menu>
  );
}
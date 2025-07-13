'use client';

import { ActionIcon, Group, Menu, CopyButton, Tooltip, Text } from '@mantine/core';
import {
  IconEdit,
  IconTrash,
  IconCopy,
  IconRefresh,
  IconGitBranch,
  IconDotsVertical,
  IconCheck
} from '@tabler/icons-react';
import type { ChatMessage } from '@/types/chat';

interface MessageActionsProps {
  message: ChatMessage;
  onEdit?: (messageId: string) => void;
  onDelete?: (messageId: string) => void;
  onRegenerate?: (messageId: string) => void;
  onBranch?: (messageId: string) => void;
  showEdit?: boolean;
  showRegenerate?: boolean;
  showBranch?: boolean;
}

export function MessageActions({
  message,
  onEdit,
  onDelete,
  onRegenerate,
  onBranch,
  showEdit = true,
  showRegenerate = false,
  showBranch = false,
}: MessageActionsProps) {
  const messageContent = typeof message.content === 'string' 
    ? message.content 
    : message.content.map(c => c.type === 'text' ? c.text : '[image]').join(' ');

  return (
    <Group gap={4}>
      <CopyButton value={messageContent} timeout={2000}>
        {({ copied, copy }) => (
          <Tooltip label={copied ? 'Copied!' : 'Copy message'} position="top">
            <ActionIcon
              variant="subtle"
              color={copied ? 'teal' : 'gray'}
              onClick={copy}
              size="sm"
            >
              {copied ? <IconCheck size={16} /> : <IconCopy size={16} />}
            </ActionIcon>
          </Tooltip>
        )}
      </CopyButton>

      {showEdit && message.role === 'user' && (
        <Tooltip label="Edit message" position="top">
          <ActionIcon
            variant="subtle"
            color="gray"
            onClick={() => onEdit?.(message.id!)}
            size="sm"
          >
            <IconEdit size={16} />
          </ActionIcon>
        </Tooltip>
      )}

      {showRegenerate && message.role === 'assistant' && (
        <Tooltip label="Regenerate response" position="top">
          <ActionIcon
            variant="subtle"
            color="gray"
            onClick={() => onRegenerate?.(message.id!)}
            size="sm"
          >
            <IconRefresh size={16} />
          </ActionIcon>
        </Tooltip>
      )}

      {showBranch && (
        <Tooltip label="Create branch" position="top">
          <ActionIcon
            variant="subtle"
            color="gray"
            onClick={() => onBranch?.(message.id!)}
            size="sm"
          >
            <IconGitBranch size={16} />
          </ActionIcon>
        </Tooltip>
      )}

      <Menu shadow="md" width={200} position="bottom-end">
        <Menu.Target>
          <ActionIcon variant="subtle" color="gray" size="sm">
            <IconDotsVertical size={16} />
          </ActionIcon>
        </Menu.Target>

        <Menu.Dropdown>
          {showEdit && message.role === 'user' && (
            <Menu.Item
              leftSection={<IconEdit size={14} />}
              onClick={() => onEdit?.(message.id!)}
            >
              Edit message
            </Menu.Item>
          )}

          {showRegenerate && message.role === 'assistant' && (
            <Menu.Item
              leftSection={<IconRefresh size={14} />}
              onClick={() => onRegenerate?.(message.id!)}
            >
              Regenerate response
            </Menu.Item>
          )}

          {showBranch && (
            <Menu.Item
              leftSection={<IconGitBranch size={14} />}
              onClick={() => onBranch?.(message.id!)}
            >
              Create branch
            </Menu.Item>
          )}

          <Menu.Divider />

          <Menu.Item
            color="red"
            leftSection={<IconTrash size={14} />}
            onClick={() => onDelete?.(message.id!)}
          >
            Delete message
          </Menu.Item>
        </Menu.Dropdown>
      </Menu>
    </Group>
  );
}
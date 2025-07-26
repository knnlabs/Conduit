import { 
  Stack, 
  Button, 
  ScrollArea, 
  Text, 
  Group, 
  ActionIcon,
  Menu,
  TextInput,
  useMantineTheme
} from '@mantine/core';
import { 
  IconPlus, 
  IconMessage, 
  IconDots, 
  IconTrash, 
  IconEdit,
  IconDownload,
  IconCopy
} from '@tabler/icons-react';
import { useState } from 'react';
import { useChatStore } from '../hooks/useChatStore';
import { formatDistanceToNow } from 'date-fns';
import { modals } from '@mantine/modals';

export function ChatSidebar() {
  const theme = useMantineTheme();
  const { 
    sessions, 
    activeSessionId, 
    createSession, 
    deleteSession, 
    setActiveSession,
    updateSessionTitle,
    clearAllSessions
  } = useChatStore();
  
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editTitle, setEditTitle] = useState('');

  const handleNewChat = () => {
    const models = sessions[0]?.model;
    if (models) {
      createSession(models);
    }
  };

  const handleDeleteSession = (sessionId: string) => {
    modals.openConfirmModal({
      title: 'Delete Chat',
      children: (
        <Text size="sm">
          Are you sure you want to delete this chat? This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => deleteSession(sessionId),
    });
  };

  const handleClearAll = () => {
    modals.openConfirmModal({
      title: 'Clear All Chats',
      children: (
        <Text size="sm">
          Are you sure you want to delete all chats? This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Clear All', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: clearAllSessions,
    });
  };

  const handleStartEdit = (sessionId: string, currentTitle: string) => {
    setEditingId(sessionId);
    setEditTitle(currentTitle);
  };

  const handleSaveEdit = () => {
    if (editingId && editTitle.trim()) {
      updateSessionTitle(editingId, editTitle.trim());
    }
    setEditingId(null);
    setEditTitle('');
  };

  const handleExportSession = (sessionId: string) => {
    const session = sessions.find(s => s.id === sessionId);
    if (!session) return;

    const dataStr = JSON.stringify(session, null, 2);
    const dataUri = `data:application/json;charset=utf-8,${ encodeURIComponent(dataStr)}`;
    
    const exportFileDefaultName = `chat-${session.title.replace(/[^a-z0-9]/gi, '-').toLowerCase()}-${new Date().toISOString().split('T')[0]}.json`;
    
    const linkElement = document.createElement('a');
    linkElement.setAttribute('href', dataUri);
    linkElement.setAttribute('download', exportFileDefaultName);
    linkElement.click();
  };

  const sortedSessions = [...sessions].sort((a, b) => 
    new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
  );

  return (
    <Stack h="100%" gap={0}>
      <Stack p="md" gap="xs">
        <Button 
          leftSection={<IconPlus size={16} />} 
          fullWidth
          onClick={handleNewChat}
          disabled={sessions.length === 0}
        >
          New Chat
        </Button>
        
        {sessions.length > 0 && (
          <Button 
            variant="subtle" 
            color="red"
            size="xs"
            onClick={handleClearAll}
          >
            Clear All
          </Button>
        )}
      </Stack>
      
      <ScrollArea style={{ flex: 1 }} px="md">
        <Stack gap={4}>
          {sortedSessions.map((session) => (
            <Group 
              key={session.id}
              gap={0}
              wrap="nowrap"
              style={{
                backgroundColor: activeSessionId === session.id 
                  ? theme.colors.blue[0] 
                  : 'transparent',
                borderRadius: theme.radius.sm,
                cursor: 'pointer',
              }}
              onClick={() => setActiveSession(session.id)}
            >
              <Stack 
                gap={2} 
                style={{ flex: 1 }} 
                p="xs"
              >
                {editingId === session.id ? (
                  <TextInput
                    value={editTitle}
                    onChange={(e) => setEditTitle(e.currentTarget.value)}
                    onBlur={handleSaveEdit}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') handleSaveEdit();
                      if (e.key === 'Escape') {
                        setEditingId(null);
                        setEditTitle('');
                      }
                    }}
                    size="xs"
                    autoFocus
                    onClick={(e) => e.stopPropagation()}
                  />
                ) : (
                  <>
                    <Group gap={4}>
                      <IconMessage size={14} />
                      <Text size="sm" lineClamp={1} fw={500}>
                        {session.title}
                      </Text>
                    </Group>
                    <Text size="xs" c="dimmed">
                      {formatDistanceToNow(new Date(session.updatedAt), { addSuffix: true })}
                    </Text>
                  </>
                )}
              </Stack>
              
              <Menu position="right-start" withinPortal>
                <Menu.Target>
                  <ActionIcon 
                    variant="subtle" 
                    size="sm"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <IconDots size={16} />
                  </ActionIcon>
                </Menu.Target>
                
                <Menu.Dropdown>
                  <Menu.Item
                    leftSection={<IconEdit size={14} />}
                    onClick={(e) => {
                      e.stopPropagation();
                      handleStartEdit(session.id, session.title);
                    }}
                  >
                    Rename
                  </Menu.Item>
                  
                  <Menu.Item
                    leftSection={<IconCopy size={14} />}
                    onClick={(e) => {
                      e.stopPropagation();
                      const newId = createSession(session.model, session.parameters);
                      sessions.find(s => s.id === session.id)?.messages.forEach(msg => {
                        const newSession = sessions.find(s => s.id === newId);
                        if (newSession) {
                          useChatStore.getState().addMessage(newId, msg);
                        }
                      });
                    }}
                  >
                    Duplicate
                  </Menu.Item>
                  
                  <Menu.Item
                    leftSection={<IconDownload size={14} />}
                    onClick={(e) => {
                      e.stopPropagation();
                      handleExportSession(session.id);
                    }}
                  >
                    Export
                  </Menu.Item>
                  
                  <Menu.Divider />
                  
                  <Menu.Item
                    color="red"
                    leftSection={<IconTrash size={14} />}
                    onClick={(e) => {
                      e.stopPropagation();
                      handleDeleteSession(session.id);
                    }}
                  >
                    Delete
                  </Menu.Item>
                </Menu.Dropdown>
              </Menu>
            </Group>
          ))}
        </Stack>
      </ScrollArea>
    </Stack>
  );
}
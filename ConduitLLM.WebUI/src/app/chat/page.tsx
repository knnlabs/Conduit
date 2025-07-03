'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Select,
  Textarea,
  Paper,
  ScrollArea,
  ActionIcon,
  Tooltip,
  Badge,
  Alert,
  Drawer,
  NumberInput,
  Switch,
  Divider,
  LoadingOverlay,
} from '@mantine/core';
import {
  IconSend,
  IconSettings,
  IconTrash,
  IconDownload,
  IconUpload,
  IconPlus,
  IconMessageCircle,
  IconRobot,
  IconUser,
  IconRefresh,
  IconCopy,
  IconCheck,
  IconAlertCircle,
} from '@tabler/icons-react';
import { useState, useRef, useEffect } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useChatStore } from '@/stores/useChatStore';
import { useChatCompletion, useStreamingChatCompletion, useAvailableModels, ChatMessage } from '@/hooks/api/useCoreApi';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';
import { safeLog } from '@/lib/utils/logging';
import { ErrorState } from '@/components/common/ErrorState';

export default function ChatPage() {
  const [message, setMessage] = useState('');
  const [useStreaming, setUseStreaming] = useState(true);
  const [settingsOpened, { open: openSettings, close: closeSettings }] = useDisclosure(false);
  const [conversationsOpened, { toggle: toggleConversations }] = useDisclosure(true);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const {
    conversations,
    activeConversationId,
    isStreaming,
    streamingMessage,
    selectedVirtualKey,
    selectedModel,
    parameters,
    systemPrompt,
    createConversation,
    deleteConversation,
    setActiveConversation,
    addMessage,
    updateMessage,
    setSelectedVirtualKey,
    setSelectedModel,
    updateParameters,
    setSystemPrompt,
    setStreaming,
    updateStreamingMessage,
    exportConversation,
    clearConversation,
  } = useChatStore();

  const { data: virtualKeys, isLoading: keysLoading, error: keysError } = useVirtualKeys();
  const { data: models, isLoading: modelsLoading, error: modelsError } = useAvailableModels();
  const chatCompletion = useChatCompletion();
  
  const streamingCompletion = useStreamingChatCompletion();

  const activeConversation = conversations.find(conv => conv.id === activeConversationId);

  // Auto-scroll to bottom when new messages arrive
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [activeConversation?.messages]);

  // Auto-focus textarea when page loads
  useEffect(() => {
    textareaRef.current?.focus();
  }, []);

  const handleSendMessage = async () => {
    if (!message.trim() || !selectedVirtualKey || !selectedModel) {
      notifications.show({
        title: 'Configuration Required',
        message: 'Please select a virtual key and model before sending messages',
        color: 'orange',
      });
      return;
    }

    let conversationId = activeConversationId;
    
    // Create new conversation if none exists
    if (!conversationId) {
      conversationId = createConversation(selectedModel);
    }

    // Add user message
    const userMessage: ChatMessage = {
      role: 'user',
      content: message.trim(),
    };
    addMessage(conversationId, userMessage);

    // Prepare messages for API
    const conversation = conversations.find(conv => conv.id === conversationId);
    const apiMessages: ChatMessage[] = [];
    
    // Add system prompt if provided
    if (systemPrompt.trim()) {
      apiMessages.push({
        role: 'system',
        content: systemPrompt.trim(),
      });
    }
    
    // Add conversation messages
    if (conversation) {
      apiMessages.push(...conversation.messages, userMessage);
    } else {
      apiMessages.push(userMessage);
    }

    setMessage('');

    try {
      if (useStreaming) {
        // Add placeholder assistant message for streaming
        const assistantMessage: ChatMessage = {
          role: 'assistant',
          content: '',
        };
        addMessage(conversationId, assistantMessage);
        setStreaming(true);

        // Start streaming completion
        await streamingCompletion.mutateAsync({
          virtualKey: selectedVirtualKey,
          model: selectedModel,
          messages: apiMessages,
          temperature: parameters.temperature,
          top_p: parameters.top_p,
          max_tokens: parameters.max_tokens,
          onChunk: (content: string) => {
            updateStreamingMessage(content);
          },
        });
      } else {
        // Standard completion request
        const response = await chatCompletion.mutateAsync({
          virtualKey: selectedVirtualKey,
          model: selectedModel,
          messages: apiMessages,
          temperature: parameters.temperature,
          top_p: parameters.top_p,
          max_tokens: parameters.max_tokens,
        });

        // Add assistant response
        if (response.choices && response.choices.length > 0) {
          const assistantMessage: ChatMessage = {
            role: 'assistant',
            content: response.choices[0].message?.content || 'No response generated',
          };
          addMessage(conversationId, assistantMessage);
        }
      }

      safeLog('Chat completion successful', { 
        model: selectedModel, 
        messageCount: apiMessages.length,
        streaming: useStreaming
      });
    } catch (error: any) {
      setStreaming(false);
      
      // Add error message to chat
      const errorMessage: ChatMessage = {
        role: 'assistant',
        content: `Error: ${error.message || 'Failed to generate response'}`,
      };
      addMessage(conversationId, errorMessage);
      
      safeLog('Chat completion failed', { error: error.message });
    }
  };

  const handleKeyPress = (event: React.KeyboardEvent) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      handleSendMessage();
    }
  };

  const handleNewConversation = () => {
    if (selectedModel) {
      createConversation(selectedModel);
    } else {
      notifications.show({
        title: 'Model Required',
        message: 'Please select a model before creating a new conversation',
        color: 'orange',
      });
    }
  };

  const handleExportConversation = () => {
    if (activeConversationId) {
      const data = exportConversation(activeConversationId);
      const blob = new Blob([data], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `conversation-${new Date().toISOString().split('T')[0]}.json`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      
      notifications.show({
        title: 'Exported',
        message: 'Conversation exported successfully',
        color: 'green',
      });
    }
  };

  const copyToClipboard = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      notifications.show({
        title: 'Copied',
        message: 'Message copied to clipboard',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Copy Failed',
        message: 'Failed to copy to clipboard',
        color: 'red',
      });
    }
  };

  const formatMessageTime = (conversation: any) => {
    return new Date(conversation.updatedAt).toLocaleTimeString();
  };

  // Show error state if critical data fails to load
  if (keysError || modelsError) {
    return (
      <Stack gap="md">
        <div>
          <Title order={1}>Chat Interface</Title>
          <Text c="dimmed">Interactive chat with LLM models</Text>
        </div>
        <ErrorState 
          error={keysError || modelsError} 
          title="Failed to load configuration"
          fullPage
        />
      </Stack>
    );
  }

  return (
    <>
      <style jsx>{`
        @keyframes blink {
          0%, 50% { opacity: 1; }
          51%, 100% { opacity: 0; }
        }
      `}</style>
      <Stack gap="md" h="calc(100vh - 120px)">
      <Group justify="space-between">
        <div>
          <Title order={1}>Chat Interface</Title>
          <Text c="dimmed">Interactive chat with LLM models</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconMessageCircle size={16} />}
            onClick={toggleConversations}
          >
            Conversations
          </Button>
          <Button
            variant="light"
            leftSection={<IconSettings size={16} />}
            onClick={openSettings}
          >
            Settings
          </Button>
          <Button
            leftSection={<IconPlus size={16} />}
            onClick={handleNewConversation}
          >
            New Chat
          </Button>
        </Group>
      </Group>

      <div style={{ display: 'flex', gap: '1rem', height: '100%' }}>
        {/* Conversations Sidebar */}
        {conversationsOpened && (
          <Paper withBorder p="md" style={{ width: '300px', height: '100%' }}>
            <Stack gap="md" h="100%">
              <Group justify="space-between">
                <Text fw={600}>Conversations</Text>
                <ActionIcon
                  variant="subtle"
                  size="sm"
                  onClick={handleNewConversation}
                >
                  <IconPlus size={16} />
                </ActionIcon>
              </Group>
              
              <ScrollArea flex={1}>
                <Stack gap="xs">
                  {conversations.map((conversation) => (
                    <Card
                      key={conversation.id}
                      p="sm"
                      withBorder={conversation.id === activeConversationId}
                      style={{
                        cursor: 'pointer',
                        borderColor: conversation.id === activeConversationId ? 'var(--mantine-color-blue-6)' : undefined,
                      }}
                      onClick={() => setActiveConversation(conversation.id)}
                    >
                      <Group justify="space-between" wrap="nowrap">
                        <div style={{ flex: 1, minWidth: 0 }}>
                          <Text size="sm" fw={500} truncate>
                            {conversation.title}
                          </Text>
                          <Text size="xs" c="dimmed">
                            {formatMessageTime(conversation)} • {conversation.messages.length} messages
                          </Text>
                          <Badge size="xs" variant="light" mt={4}>
                            {conversation.model}
                          </Badge>
                        </div>
                        <ActionIcon
                          size="sm"
                          color="red"
                          variant="subtle"
                          onClick={(e) => {
                            e.stopPropagation();
                            deleteConversation(conversation.id);
                          }}
                        >
                          <IconTrash size={14} />
                        </ActionIcon>
                      </Group>
                    </Card>
                  ))}
                  
                  {conversations.length === 0 && (
                    <Text size="sm" c="dimmed" ta="center" py="xl">
                      No conversations yet. Start a new chat!
                    </Text>
                  )}
                </Stack>
              </ScrollArea>
            </Stack>
          </Paper>
        )}

        {/* Main Chat Area */}
        <Card withBorder flex={1} p={0} style={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
          {/* Chat Header */}
          <Group justify="space-between" p="md" style={{ borderBottom: '1px solid var(--mantine-color-gray-3)' }}>
            <div>
              <Text fw={600}>
                {activeConversation?.title || 'Select or create a conversation'}
              </Text>
              {activeConversation && (
                <Text size="sm" c="dimmed">
                  Model: {activeConversation.model} • {activeConversation.messages.length} messages
                </Text>
              )}
            </div>
            
            {activeConversation && (
              <Group gap="xs">
                <Tooltip label="Export conversation">
                  <ActionIcon variant="subtle" onClick={handleExportConversation}>
                    <IconDownload size={16} />
                  </ActionIcon>
                </Tooltip>
                <Tooltip label="Clear conversation">
                  <ActionIcon 
                    variant="subtle" 
                    color="red"
                    onClick={() => clearConversation(activeConversation.id)}
                  >
                    <IconTrash size={16} />
                  </ActionIcon>
                </Tooltip>
              </Group>
            )}
          </Group>

          {/* Messages Area */}
          <ScrollArea flex={1} p="md">
            <Stack gap="md">
              {activeConversation?.messages.map((msg, index) => {
                const isLastMessage = index === activeConversation.messages.length - 1;
                const isStreamingThisMessage = isStreaming && isLastMessage && msg.role === 'assistant';
                const displayContent = isStreamingThisMessage ? streamingMessage : msg.content;
                
                return (
                  <Group key={index} align="flex-start" gap="md">
                    <ActionIcon
                      variant="light"
                      color={msg.role === 'user' ? 'blue' : msg.role === 'assistant' ? 'green' : 'gray'}
                      size="sm"
                    >
                      {msg.role === 'user' ? <IconUser size={16} /> : <IconRobot size={16} />}
                    </ActionIcon>
                    
                    <Paper
                      p="md"
                      withBorder
                      style={{ flex: 1, maxWidth: '80%' }}
                      bg={msg.role === 'user' ? 'blue.0' : 'gray.0'}
                    >
                      <Group justify="space-between" mb="xs">
                        <Group gap="xs">
                          <Badge size="xs" variant="light">
                            {msg.role}
                          </Badge>
                          {isStreamingThisMessage && (
                            <Badge size="xs" variant="light" color="blue">
                              streaming...
                            </Badge>
                          )}
                        </Group>
                        <ActionIcon
                          size="xs"
                          variant="subtle"
                          onClick={() => copyToClipboard(displayContent)}
                          disabled={isStreamingThisMessage && !displayContent}
                        >
                          <IconCopy size={12} />
                        </ActionIcon>
                      </Group>
                      <Text style={{ whiteSpace: 'pre-wrap' }}>
                        {displayContent}
                        {isStreamingThisMessage && displayContent && (
                          <span style={{ 
                            display: 'inline-block',
                            width: '8px',
                            height: '16px',
                            backgroundColor: 'var(--mantine-color-blue-6)',
                            marginLeft: '2px',
                            animation: 'blink 1s infinite'
                          }} />
                        )}
                      </Text>
                    </Paper>
                  </Group>
                );
              })}
              
              {activeConversation?.messages.length === 0 && (
                <Alert icon={<IconMessageCircle size={16} />} variant="light">
                  <Text>Start the conversation by typing a message below.</Text>
                </Alert>
              )}
              
              <div ref={messagesEndRef} />
            </Stack>
          </ScrollArea>

          {/* Message Input */}
          <div style={{ padding: '1rem', borderTop: '1px solid var(--mantine-color-gray-3)' }}>
            <Stack gap="md">
              {!selectedVirtualKey && (
                <Alert icon={<IconAlertCircle size={16} />} color="orange">
                  Please select a virtual key in the settings to start chatting.
                </Alert>
              )}
              
              <Group align="flex-end">
                <Textarea
                  ref={textareaRef}
                  flex={1}
                  placeholder="Type your message... (Press Enter to send, Shift+Enter for new line)"
                  value={message}
                  onChange={(event) => setMessage(event.currentTarget.value)}
                  onKeyDown={handleKeyPress}
                  disabled={!selectedVirtualKey || !selectedModel || chatCompletion.isPending || streamingCompletion.isPending || isStreaming}
                  autosize
                  minRows={1}
                  maxRows={6}
                />
                <Button
                  leftSection={<IconSend size={16} />}
                  onClick={handleSendMessage}
                  disabled={!message.trim() || !selectedVirtualKey || !selectedModel || isStreaming}
                  loading={chatCompletion.isPending || streamingCompletion.isPending || isStreaming}
                >
                  Send
                </Button>
              </Group>
            </Stack>
          </div>
        </Card>
      </div>

      {/* Settings Drawer */}
      <Drawer
        opened={settingsOpened}
        onClose={closeSettings}
        title="Chat Settings"
        position="right"
        size="md"
      >
        <Stack gap="md">
          <div>
            <Text fw={600} mb="xs">Configuration</Text>
            
            <Stack gap="md">
              <Select
                label="Virtual Key"
                placeholder="Select a virtual key"
                data={virtualKeys?.map((key: any) => ({
                  value: key.id,
                  label: key.keyName,
                })) || []}
                value={selectedVirtualKey}
                onChange={(value) => setSelectedVirtualKey(value || '')}
                disabled={keysLoading}
              />

              <Select
                label="Model"
                placeholder="Select a model"
                data={models?.map((model: any) => ({
                  value: model.id,
                  label: model.id,
                })) || []}
                value={selectedModel}
                onChange={(value) => setSelectedModel(value || '')}
                disabled={!selectedVirtualKey || modelsLoading}
              />
            </Stack>
          </div>

          <Divider />

          <div>
            <Text fw={600} mb="xs">Parameters</Text>
            
            <Stack gap="md">
              <NumberInput
                label="Temperature"
                description="Controls randomness (0.0 = focused, 1.0 = creative)"
                value={parameters.temperature}
                onChange={(value) => updateParameters({ temperature: value as number })}
                min={0}
                max={2}
                step={0.1}
                decimalScale={1}
              />

              <NumberInput
                label="Top P"
                description="Nucleus sampling parameter"
                value={parameters.top_p}
                onChange={(value) => updateParameters({ top_p: value as number })}
                min={0}
                max={1}
                step={0.1}
                decimalScale={1}
              />

              <NumberInput
                label="Max Tokens"
                description="Maximum response length"
                value={parameters.max_tokens}
                onChange={(value) => updateParameters({ max_tokens: value as number })}
                min={1}
                max={4000}
                step={1}
              />
              
              <Switch
                label="Enable Streaming"
                description="Stream responses in real-time for faster interaction"
                checked={useStreaming}
                onChange={(event) => setUseStreaming(event.currentTarget.checked)}
              />
            </Stack>
          </div>

          <Divider />

          <div>
            <Text fw={600} mb="xs">System Prompt</Text>
            <Textarea
              placeholder="Enter a system prompt to guide the AI's behavior..."
              value={systemPrompt}
              onChange={(event) => setSystemPrompt(event.currentTarget.value)}
              autosize
              minRows={3}
              maxRows={8}
            />
          </div>
        </Stack>
      </Drawer>
    </Stack>
    </>
  );
}
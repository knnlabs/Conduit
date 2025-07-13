'use client';

import { useEffect, useState, useCallback, useRef } from 'react';
import {
  Container,
  Paper,
  Stack,
  Grid,
  Text,
  Center,
  Loader,
  Alert,
  Select,
  Group,
  Badge,
  ScrollArea,
  Button,
  Divider,
} from '@mantine/core';
import { IconAlertCircle, IconRobot, IconUser, IconBolt, IconClock, IconSettings } from '@tabler/icons-react';
import { v4 as uuidv4 } from 'uuid';
import { ContentHelpers, type TextContent, type ImageContent } from '@knn_labs/conduit-core-client';
import { ChatInput } from './ChatInput';
import { ChatMessages } from './ChatMessages';
import { MessageActions } from './MessageActions';
import { MessageEditor } from './MessageEditor';
import { ConversationExport } from './ConversationExport';
import { AdvancedChatControls, type ChatParameters } from './AdvancedChatControls';
import { TokenCounter } from './TokenCounter';
import { CodeBlock } from './CodeBlock';
import { MarkdownRenderer } from './MarkdownRenderer';
import { ImageUploadArea } from './ImageUploadArea';
import { StreamingControls } from './StreamingControls';
import { ConnectionStatus } from './ConnectionStatus';
import { useWebSocketChat } from '../hooks/useWebSocketChat';
import { ImageAttachment } from '../types';
import type { ChatMessage, Conversation } from '@/types/chat';

interface EnhancedMessage extends ChatMessage {
  isEditing?: boolean;
  streamingMetadata?: {
    startTime: number;
    tokenCount: number;
    tps: number;
  };
}

const DEFAULT_CHAT_PARAMS: ChatParameters = {
  temperature: 0.7,
  topP: 1,
  maxTokens: 2048,
  frequencyPenalty: 0,
  presencePenalty: 0,
  systemPrompt: '',
  stopSequences: [],
};

export function ChatInterfaceEnhanced() {
  const [models, setModels] = useState<Array<{ value: string; label: string; supportsVision?: boolean }>>([]);
  const [selectedModel, setSelectedModel] = useState<string | null>(null);
  const [messages, setMessages] = useState<EnhancedMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [modelsLoading, setModelsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const [tokensPerSecond, setTokensPerSecond] = useState<number | null>(null);
  const [chatParams, setChatParams] = useState<ChatParameters>(DEFAULT_CHAT_PARAMS);
  const [conversationId] = useState(() => uuidv4());
  const [streamController, setStreamController] = useState<AbortController | null>(null);
  const [isStreamPaused, setIsStreamPaused] = useState(false);
  const streamStartTime = useRef<number>(0);
  const streamTokenCount = useRef<number>(0);

  // WebSocket connection (stub for now)
  const websocket = useWebSocketChat({
    conversationId,
    onConnectionChange: (status) => {
      console.log('WebSocket status:', status);
    },
  });

  // Fetch models on mount
  useEffect(() => {
    const fetchModels = async () => {
      try {
        const response = await fetch('/api/model-mappings');
        if (!response.ok) {
          throw new Error('Failed to fetch models');
        }
        const data = await response.json();
        const modelOptions = data.map((m: any) => ({
          value: m.modelId,
          label: `${m.modelId} (${m.providerId})`,
          supportsVision: m.supportsVision || false
        }));
        setModels(modelOptions);
        if (modelOptions.length > 0) {
          setSelectedModel(modelOptions[0].value);
        }
      } catch (err) {
        setError('Failed to load models');
        console.error(err);
      } finally {
        setModelsLoading(false);
      }
    };

    fetchModels();
  }, []);

  const buildMessageContent = (text: string, images?: ImageAttachment[]) => {
    if (!images || images.length === 0) {
      return text;
    }

    const content: Array<TextContent | ImageContent> = [];
    
    if (text) {
      content.push(ContentHelpers.text(text));
    }

    images.forEach((img) => {
      if (img.base64) {
        content.push(ContentHelpers.imageUrl(img.base64));
      }
    });

    return content;
  };

  const handleStreamPause = () => {
    setIsStreamPaused(true);
    // TODO: Implement actual pause logic with controllable streams
  };

  const handleStreamResume = () => {
    setIsStreamPaused(false);
    // TODO: Implement actual resume logic
  };

  const handleStreamCancel = () => {
    if (streamController) {
      streamController.abort();
      setStreamController(null);
    }
    setIsLoading(false);
    setStreamingContent('');
  };

  const sendMessage = useCallback(async (inputMessage: string, images?: ImageAttachment[]) => {
    if (!inputMessage.trim() && (!images || images.length === 0)) return;
    if (!selectedModel || isLoading) return;

    const userMessage: EnhancedMessage = {
      id: uuidv4(),
      role: 'user',
      content: buildMessageContent(inputMessage, images),
      metadata: {
        timestamp: new Date().toISOString()
      }
    };

    setMessages(prev => [...prev, userMessage]);
    setIsLoading(true);
    setStreamingContent('');
    setTokensPerSecond(null);
    streamStartTime.current = Date.now();
    streamTokenCount.current = 0;

    const controller = new AbortController();
    setStreamController(controller);

    try {
      const requestBody = {
        messages: [...messages, userMessage].map(m => ({
          role: m.role,
          content: m.content
        })),
        model: selectedModel,
        stream: true,
        temperature: chatParams.temperature,
        top_p: chatParams.topP,
        max_tokens: chatParams.maxTokens,
        frequency_penalty: chatParams.frequencyPenalty,
        presence_penalty: chatParams.presencePenalty,
        stop: chatParams.stopSequences.length > 0 ? chatParams.stopSequences : undefined,
      };

      if (chatParams.systemPrompt) {
        requestBody.messages.unshift({
          role: 'system',
          content: chatParams.systemPrompt
        });
      }

      const response = await fetch('/api/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
        signal: controller.signal,
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let fullContent = '';
      let lastTpsUpdate = Date.now();

      const assistantMessage: EnhancedMessage = {
        id: uuidv4(),
        role: 'assistant',
        content: '',
        metadata: {
          model: selectedModel,
          timestamp: new Date().toISOString()
        },
        streamingMetadata: {
          startTime: Date.now(),
          tokenCount: 0,
          tps: 0
        }
      };

      setMessages(prev => [...prev, assistantMessage]);

      while (reader) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6);
            if (data === '[DONE]') {
              break;
            }
            try {
              const parsed = JSON.parse(data);
              const content = parsed.choices?.[0]?.delta?.content || '';
              if (content) {
                fullContent += content;
                streamTokenCount.current++;
                setStreamingContent(fullContent);

                // Update TPS every 500ms
                const now = Date.now();
                if (now - lastTpsUpdate > 500) {
                  const elapsed = (now - streamStartTime.current) / 1000;
                  const tps = streamTokenCount.current / elapsed;
                  setTokensPerSecond(tps);
                  lastTpsUpdate = now;
                }
              }
            } catch (e) {
              console.error('Error parsing SSE data:', e);
            }
          }
        }
      }

      // Final message update
      const finalTps = streamTokenCount.current / ((Date.now() - streamStartTime.current) / 1000);
      setMessages(prev => prev.map(msg => 
        msg.id === assistantMessage.id 
          ? { 
              ...msg, 
              content: fullContent,
              metadata: {
                ...msg.metadata,
                tokenCount: streamTokenCount.current,
                tps: finalTps
              }
            }
          : msg
      ));

    } catch (err: any) {
      if (err.name !== 'AbortError') {
        setError(err.message || 'Failed to send message');
        console.error('Chat error:', err);
      }
    } finally {
      setIsLoading(false);
      setStreamingContent('');
      setStreamController(null);
    }
  }, [selectedModel, messages, isLoading, chatParams]);

  const handleEditMessage = (messageId: string) => {
    setMessages(prev => prev.map(msg => 
      msg.id === messageId ? { ...msg, isEditing: true } : msg
    ));
  };

  const handleSaveEdit = async (messageId: string, newContent: string) => {
    setMessages(prev => prev.map(msg => 
      msg.id === messageId 
        ? { ...msg, content: newContent, isEditing: false }
        : msg
    ));
    
    // TODO: If this was a user message, potentially regenerate assistant response
  };

  const handleCancelEdit = (messageId: string) => {
    setMessages(prev => prev.map(msg => 
      msg.id === messageId ? { ...msg, isEditing: false } : msg
    ));
  };

  const handleDeleteMessage = (messageId: string) => {
    setMessages(prev => prev.filter(msg => msg.id !== messageId));
  };

  const handleRegenerateMessage = async (messageId: string) => {
    // Find the message and all messages before it
    const messageIndex = messages.findIndex(msg => msg.id === messageId);
    if (messageIndex === -1) return;

    // Remove this message and all after it
    const previousMessages = messages.slice(0, messageIndex);
    setMessages(previousMessages);

    // Find the last user message
    const lastUserMessage = [...previousMessages].reverse().find(msg => msg.role === 'user');
    if (lastUserMessage && typeof lastUserMessage.content === 'string') {
      await sendMessage(lastUserMessage.content);
    }
  };

  const handleExportConversation = async (format: string, options: any) => {
    const conversation: Conversation = {
      id: conversationId,
      title: `Chat ${new Date().toLocaleDateString()}`,
      model: selectedModel || 'unknown',
      messages: messages,
      metadata: chatParams,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    // TODO: Implement actual export logic using the conversation service
    console.log('Export conversation:', { format, options, conversation });
  };

  if (modelsLoading) {
    return (
      <Center h="100vh">
        <Loader size="lg" />
      </Center>
    );
  }

  const currentModel = models.find(m => m.value === selectedModel);
  const supportsVision = currentModel?.supportsVision || false;
  const elapsedTime = isLoading && streamStartTime.current 
    ? (Date.now() - streamStartTime.current) / 1000 
    : 0;

  return (
    <Container size="lg" py="md" style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
      <Stack style={{ flex: 1, height: '100%' }} gap="md">
        {/* Header */}
        <Paper shadow="xs" p="md" withBorder>
          <Stack gap="sm">
            <Group justify="space-between" align="flex-start">
              <div>
                <Text size="xl" fw={700}>AI Chat Assistant</Text>
                <Text size="sm" c="dimmed">Powered by Conduit</Text>
              </div>
              <Group>
                <ConnectionStatus 
                  status={websocket.status}
                  activeUsers={websocket.activeUsers}
                  onReconnect={websocket.reconnect}
                />
                <ConversationExport
                  conversation={{ 
                    id: conversationId, 
                    title: 'Current Chat',
                    model: selectedModel || '',
                    messages,
                    createdAt: new Date().toISOString(),
                    updatedAt: new Date().toISOString()
                  }}
                  onExport={handleExportConversation}
                />
              </Group>
            </Group>

            <Grid>
              <Grid.Col span={{ base: 12, md: 8 }}>
                <Select
                  label="Model"
                  placeholder="Select a model"
                  data={models}
                  value={selectedModel}
                  onChange={setSelectedModel}
                  disabled={isLoading}
                  rightSection={supportsVision && <Badge size="xs" variant="dot">Vision</Badge>}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 4 }}>
                <TokenCounter
                  messages={messages}
                  model={selectedModel || undefined}
                  maxTokens={chatParams.maxTokens}
                  compact
                />
              </Grid.Col>
            </Grid>
          </Stack>
        </Paper>

        {/* Advanced Controls */}
        <AdvancedChatControls
          parameters={chatParams}
          onChange={setChatParams}
          onReset={() => setChatParams(DEFAULT_CHAT_PARAMS)}
        />

        {/* Error Alert */}
        {error && (
          <Alert icon={<IconAlertCircle size={16} />} color="red" onClose={() => setError(null)} withCloseButton>
            {error}
          </Alert>
        )}

        {/* Messages */}
        <Paper shadow="xs" p="md" withBorder style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
          <ScrollArea style={{ flex: 1 }} type="auto">
            {messages.length === 0 ? (
              <Center h={200}>
                <Stack align="center" gap="xs">
                  <IconRobot size={48} style={{ color: 'var(--mantine-color-dimmed)' }} />
                  <Text c="dimmed">Start a conversation by typing a message below</Text>
                </Stack>
              </Center>
            ) : (
              <Stack gap="md">
                {messages.map((message) => (
                  <div key={message.id}>
                    <Group justify="space-between" align="flex-start" mb="xs">
                      <Group gap="xs">
                        {message.role === 'user' ? <IconUser size={20} /> : <IconRobot size={20} />}
                        <Text fw={500}>{message.role === 'user' ? 'You' : 'Assistant'}</Text>
                        {message.metadata?.timestamp && (
                          <Text size="xs" c="dimmed">
                            {new Date(message.metadata.timestamp).toLocaleTimeString()}
                          </Text>
                        )}
                        {message.metadata?.tps && (
                          <Badge size="xs" variant="light" leftSection={<IconBolt size={12} />}>
                            {message.metadata.tps.toFixed(1)} TPS
                          </Badge>
                        )}
                      </Group>
                      {!message.isEditing && (
                        <MessageActions
                          message={message}
                          onEdit={handleEditMessage}
                          onDelete={handleDeleteMessage}
                          onRegenerate={handleRegenerateMessage}
                          showRegenerate={message.role === 'assistant'}
                        />
                      )}
                    </Group>
                    
                    {message.isEditing ? (
                      <MessageEditor
                        message={message}
                        onSave={handleSaveEdit}
                        onCancel={() => handleCancelEdit(message.id!)}
                      />
                    ) : (
                      <Paper p="sm" withBorder>
                        {typeof message.content === 'string' ? (
                          <MarkdownRenderer content={message.content} />
                        ) : (
                          <Stack gap="sm">
                            {message.content.map((item, idx) => (
                              item.type === 'text' ? (
                                <MarkdownRenderer key={idx} content={item.text || ''} />
                              ) : (
                                <img 
                                  key={idx} 
                                  src={item.image_url?.url} 
                                  alt="" 
                                  style={{ maxWidth: '100%', borderRadius: '4px' }}
                                />
                              )
                            ))}
                          </Stack>
                        )}
                      </Paper>
                    )}
                  </div>
                ))}
                
                {isLoading && streamingContent && (
                  <div>
                    <Group gap="xs" mb="xs">
                      <IconRobot size={20} />
                      <Text fw={500}>Assistant</Text>
                      <Loader size="xs" />
                    </Group>
                    <Paper p="sm" withBorder>
                      <MarkdownRenderer content={streamingContent} />
                    </Paper>
                  </div>
                )}
              </Stack>
            )}
          </ScrollArea>
        </Paper>

        {/* Streaming Controls */}
        {isLoading && (
          <StreamingControls
            isStreaming={isLoading}
            isPaused={isStreamPaused}
            tokenCount={streamTokenCount.current}
            tps={tokensPerSecond || 0}
            elapsedTime={elapsedTime}
            onPause={handleStreamPause}
            onResume={handleStreamResume}
            onCancel={handleStreamCancel}
          />
        )}

        {/* Input */}
        <Paper shadow="xs" p="md" withBorder>
          <ChatInput
            onSendMessage={sendMessage}
            isStreaming={isLoading}
            onStopStreaming={handleStreamCancel}
            disabled={!selectedModel}
            model={currentModel ? { 
              id: currentModel.value,
              providerId: currentModel.label.split(' ')[1]?.slice(1, -1) || '',
              displayName: currentModel.label,
              supportsVision: currentModel.supportsVision,
              supportsFunctionCalling: false,
              supportsToolUsage: false,
              supportsJsonMode: true,
              supportsStreaming: true,
              maxContextTokens: chatParams.maxTokens
            } : undefined}
          />
        </Paper>
      </Stack>
    </Container>
  );
}
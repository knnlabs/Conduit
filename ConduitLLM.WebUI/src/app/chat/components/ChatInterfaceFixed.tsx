'use client';

import { useEffect, useState, useCallback } from 'react';
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
  ScrollArea
} from '@mantine/core';
import { IconAlertCircle, IconRobot, IconUser, IconBolt, IconClock } from '@tabler/icons-react';
import { v4 as uuidv4 } from 'uuid';
import { ContentHelpers, type TextContent, type ImageContent } from '@knn_labs/conduit-core-client';
import { ChatInput } from './ChatInput';
import { ChatMessages } from './ChatMessages';
import { ImageAttachment } from '../types';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  images?: ImageAttachment[];
  timestamp: Date;
  metadata?: {
    tokensUsed?: number;
    tokensPerSecond?: number;
    latency?: number;
  };
}

export function ChatInterfaceFixed() {
  const [models, setModels] = useState<Array<{ value: string; label: string; supportsVision?: boolean }>>([]);
  const [selectedModel, setSelectedModel] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [modelsLoading, setModelsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const [tokensPerSecond, setTokensPerSecond] = useState<number | null>(null);

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

  const sendMessage = useCallback(async (inputMessage: string, images?: ImageAttachment[]) => {
    if (!inputMessage.trim() && (!images || images.length === 0)) return;
    if (!selectedModel || isLoading) return;

    const userMessage: Message = {
      id: uuidv4(),
      role: 'user',
      content: inputMessage.trim(),
      images,
      timestamp: new Date()
    };

    setMessages(prev => [...prev, userMessage]);
    setIsLoading(true);
    setStreamingContent('');
    setTokensPerSecond(null);

    try {
      // Convert message format for API
      const messageContent = buildMessageContent(inputMessage, images);
      
      const requestBody = {
        messages: [...messages, userMessage].map(m => ({
          role: m.role,
          content: m.images && m.images.length > 0 
            ? buildMessageContent(m.content, m.images)
            : m.content
        })),
        model: selectedModel,
        stream: true
      };
      
      console.log('Sending chat request:', {
        model: selectedModel,
        messageCount: requestBody.messages.length,
        lastMessage: requestBody.messages[requestBody.messages.length - 1]
      });
      
      const response = await fetch('/api/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error('Chat API error:', response.status, errorText);
        throw new Error(`Failed to get response: ${response.status} ${response.statusText}`);
      }

      // Handle streaming response
      const reader = response.body?.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let fullContent = '';

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
              const assistantMessage: Message = {
                id: uuidv4(),
                role: 'assistant',
                content: fullContent,
                timestamp: new Date(),
                metadata: {
                  // Metrics will be populated from server-sent performance data
                }
              };

              setMessages(prev => [...prev, assistantMessage]);
              setStreamingContent('');
              setTokensPerSecond(null);
              break;
            }

            try {
              const parsed = JSON.parse(data);
              const delta = parsed.choices?.[0]?.delta;
              
              if (delta?.content) {
                fullContent += delta.content;
                setStreamingContent(fullContent);
              }

              // Check for performance metrics in the chunk
              if (parsed.performance?.tokens_per_second) {
                setTokensPerSecond(parsed.performance.tokens_per_second);
              }
            } catch (e) {
              console.error('Failed to parse SSE data:', e);
            }
          }
        }
      }
    } catch (err: any) {
      console.error('Chat error:', err);
      const errorMessage = err.message || 'Failed to send message';
      setError(errorMessage);
      
      // Show error as a system message in chat
      const errorMsg: Message = {
        id: uuidv4(),
        role: 'assistant',
        content: `Error: ${errorMessage}`,
        timestamp: new Date(),
        metadata: {
          tokensUsed: 0,
          tokensPerSecond: 0,
          latency: 0,
        }
      };
      setMessages(prev => [...prev, errorMsg]);
    } finally {
      setIsLoading(false);
      setStreamingContent('');
      setTokensPerSecond(null);
    }
  }, [selectedModel, messages, isLoading]);

  const buildMessageContent = (text: string, images?: ImageAttachment[]) => {
    if (!images || images.length === 0) {
      return text;
    }

    const content: Array<TextContent | ImageContent> = [];
    
    if (text) {
      content.push(ContentHelpers.text(text));
    }

    images.forEach(img => {
      if (img.base64) {
        content.push(ContentHelpers.imageBase64(img.base64, img.mimeType));
      } else if (img.url) {
        content.push(ContentHelpers.imageUrl(img.url));
      }
    });

    return content;
  };

  const currentModel = models.find(m => m.value === selectedModel);

  if (modelsLoading) {
    return (
      <Center h="100vh">
        <Loader size="lg" />
      </Center>
    );
  }

  if (error) {
    return (
      <Container size="sm" mt="xl">
        <Alert icon={<IconAlertCircle size={16} />} color="red" title="Error">
          {error}
        </Alert>
      </Container>
    );
  }

  if (models.length === 0) {
    return (
      <Container size="sm" mt="xl">
        <Alert icon={<IconAlertCircle size={16} />} color="yellow" title="No models available">
          No models are currently configured. Please configure model mappings first.
        </Alert>
      </Container>
    );
  }

  return (
    <Container size="lg" py="md">
      <Stack h="calc(100vh - 100px)">
        <Paper p="md" withBorder>
          <Group justify="space-between">
            <Select
              label="Model"
              placeholder="Select a model"
              value={selectedModel}
              onChange={setSelectedModel}
              data={models}
              style={{ flex: 1, maxWidth: 400 }}
            />
            {currentModel?.supportsVision && (
              <Badge variant="light" color="blue">
                Vision Enabled
              </Badge>
            )}
          </Group>
        </Paper>

        <Paper p="md" withBorder style={{ flex: 1, overflow: 'hidden' }}>
          <ChatMessages 
            messages={messages}
            streamingContent={isLoading ? streamingContent : undefined}
            tokensPerSecond={tokensPerSecond}
          />
        </Paper>

        <Paper p="md" withBorder>
          <ChatInput
            onSendMessage={sendMessage}
            isStreaming={isLoading}
            onStopStreaming={() => {}}
            disabled={!selectedModel}
            model={currentModel ? {
              id: currentModel.value,
              providerId: '',
              displayName: currentModel.label,
              supportsVision: currentModel.supportsVision
            } : undefined}
          />
        </Paper>
      </Stack>
    </Container>
  );
}
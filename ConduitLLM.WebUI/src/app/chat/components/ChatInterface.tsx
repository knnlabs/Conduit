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
import { StreamingPerformanceMetrics, UsageData, MessageMetrics, SSEEventType, MetricsEventData } from '../types/metrics';
import { parseSSEStream } from '../utils/sse-parser';
import { usePerformanceSettings } from '../hooks/usePerformanceSettings';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  images?: ImageAttachment[];
  timestamp: Date;
  metadata?: MessageMetrics;
}

export function ChatInterface() {
  const [models, setModels] = useState<Array<{ value: string; label: string; supportsVision?: boolean }>>([]);
  const [selectedModel, setSelectedModel] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [modelsLoading, setModelsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const [tokensPerSecond, setTokensPerSecond] = useState<number | null>(null);
  
  const performanceSettings = usePerformanceSettings();

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
      if (!reader) throw new Error('No response body reader');
      
      let fullContent = '';
      let startTime = Date.now();
      let finalMetrics: Partial<StreamingPerformanceMetrics & UsageData & MetricsEventData> = {};

      // Process SSE stream with proper event parsing
      for await (const event of parseSSEStream(reader)) {
        if (event.data === '[DONE]') {
          const endTime = Date.now();
          const duration = (endTime - startTime) / 1000;
          
          const metadata: MessageMetrics | undefined = performanceSettings.trackPerformanceMetrics
            ? {
                tokensUsed: finalMetrics.total_tokens || finalMetrics.completion_tokens || finalMetrics.tokens_generated || 0,
                tokensPerSecond: finalMetrics.tokens_per_second || finalMetrics.current_tokens_per_second || 0,
                latency: finalMetrics.total_latency_ms || duration * 1000,
              }
            : undefined;
          
          const assistantMessage: Message = {
            id: uuidv4(),
            role: 'assistant',
            content: fullContent,
            timestamp: new Date(),
            metadata
          };

          setMessages(prev => [...prev, assistantMessage]);
          setStreamingContent('');
          setTokensPerSecond(null);
          break;
        }

        switch (event.event) {
          case SSEEventType.Content:
            // Handle content chunks
            const delta = event.data?.choices?.[0]?.delta;
            
            if (delta?.content) {
              fullContent += delta.content;
              setStreamingContent(fullContent);
            }

            // Check for inline performance metrics (backward compatibility)
            if (event.data?.performance && performanceSettings.useServerMetrics) {
              Object.assign(finalMetrics, event.data.performance);
              if (event.data.performance.tokens_per_second && performanceSettings.showTokensPerSecond) {
                setTokensPerSecond(event.data.performance.tokens_per_second);
              }
            }
            
            // Check for usage data (OpenAI style)
            if (event.data?.usage) {
              Object.assign(finalMetrics, event.data.usage);
            }
            break;
            
          case SSEEventType.Metrics:
            // Handle live metrics updates
            if (performanceSettings.useServerMetrics) {
              Object.assign(finalMetrics, event.data);
              if (event.data.current_tokens_per_second && performanceSettings.showTokensPerSecond) {
                setTokensPerSecond(event.data.current_tokens_per_second);
              }
            }
            break;
            
          case SSEEventType.MetricsFinal:
            // Handle final metrics
            if (performanceSettings.useServerMetrics) {
              Object.assign(finalMetrics, event.data);
            }
            break;
            
          case SSEEventType.Error:
            // Handle error events
            console.error('SSE Error event:', event.data);
            break;
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
            tokensPerSecond={performanceSettings.showTokensPerSecond ? tokensPerSecond : null}
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
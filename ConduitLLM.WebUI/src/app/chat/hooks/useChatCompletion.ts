import { useState, useCallback, useRef } from 'react';
import { useCoreApi } from '@/hooks/useCoreApi';
import { ChatMessage, FunctionDefinition, ToolDefinition, ToolChoice } from '../types';
import { notifications } from '@mantine/notifications';
import { v4 as uuidv4 } from 'uuid';

interface UseChatCompletionOptions {
  onStreamStart?: () => void;
  onStreamEnd?: (message: ChatMessage) => void;
  onStreamError?: (error: Error) => void;
  onTokensPerSecond?: (tps: number) => void;
}

export function useChatCompletion(options: UseChatCompletionOptions = {}) {
  const [isStreaming, setIsStreaming] = useState(false);
  const [streamingContent, setStreamingContent] = useState('');
  const abortControllerRef = useRef<AbortController | null>(null);
  const coreApi = useCoreApi();
  const startTimeRef = useRef<number>(0);
  const tokenCountRef = useRef<number>(0);

  const stopStreaming = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
    setIsStreaming(false);
    setStreamingContent('');
  }, []);

  const sendMessage = useCallback(
    async (
      messages: Array<{ role: 'user' | 'assistant' | 'system' | 'function'; content: string; name?: string }>,
      model: string,
      parameters: {
        temperature?: number;
        maxTokens?: number;
        topP?: number;
        frequencyPenalty?: number;
        presencePenalty?: number;
        responseFormat?: { type: 'text' | 'json_object' };
        stream?: boolean;
        functions?: FunctionDefinition[];
        tools?: ToolDefinition[];
        toolChoice?: ToolChoice;
        seed?: number;
        stop?: string[];
      } = {}
    ) => {
      try {
        const controller = new AbortController();
        abortControllerRef.current = controller;

        const { stream = true, ...otherParams } = parameters;

        if (stream) {
          setIsStreaming(true);
          setStreamingContent('');
          startTimeRef.current = Date.now();
          tokenCountRef.current = 0;
          options.onStreamStart?.();

          const response = await fetch('/api/chat/completions', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({
              messages,
              model,
              stream: true,
              temperature: otherParams.temperature,
              max_tokens: otherParams.maxTokens,
              top_p: otherParams.topP,
              ['frequency_penalty']: otherParams.frequencyPenalty,
              presence_penalty: otherParams.presencePenalty,
              response_format: otherParams.responseFormat,
              functions: otherParams.functions,
              tools: otherParams.tools,
              ['tool_choice']: otherParams.toolChoice,
              seed: otherParams.seed,
              stop: otherParams.stop,
            }),
            signal: controller.signal,
          });

          if (!response.ok) {
            const error = await response.json() as { error?: string };
            throw new Error(error.error ?? 'Failed to send message');
          }

          const reader = response.body?.getReader();
          const decoder = new TextDecoder();
          let buffer = '';
          let fullContent = '';
          let functionCall: { name: string; arguments: string } | null = null;
          const toolCalls: Array<{ id: string; type: 'function'; function: { name: string; arguments: string } }> = [];

          while (reader) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop() ?? '';

            for (const line of lines) {
              if (line.startsWith('data: ')) {
                const data = line.slice(6);
                if (data === '[DONE]') {
                  setIsStreaming(false);
                  const endTime = Date.now();
                  const duration = (endTime - startTimeRef.current) / 1000;
                  const tokensPerSecond = tokenCountRef.current / duration;
                  
                  options.onTokensPerSecond?.(tokensPerSecond);
                  
                  const completedMessage: ChatMessage = {
                    id: uuidv4(),
                    role: 'assistant',
                    content: fullContent,
                    timestamp: new Date(),
                    model,
                    functionCall: functionCall ?? undefined,
                    toolCalls: toolCalls.length > 0 ? toolCalls : undefined,
                    metadata: {
                      tokensUsed: tokenCountRef.current,
                      tokensPerSecond,
                      latency: duration * 1000,
                    },
                  };
                  
                  options.onStreamEnd?.(completedMessage);
                  return completedMessage;
                }

                try {
                  interface SSEStreamData {
                    choices?: Array<{
                      delta?: {
                        content?: string;
                        ['function_call']?: {
                          name?: string;
                          arguments?: string;
                        };
                        ['tool_calls']?: Array<{
                          index: number;
                          id: string;
                          function?: {
                            name?: string;
                            arguments?: string;
                          };
                        }>;
                      };
                    }>;
                  }
                  const parsed = JSON.parse(data) as SSEStreamData;
                  const delta = parsed.choices?.[0]?.delta;
                  
                  if (delta?.content) {
                    fullContent += delta.content;
                    setStreamingContent(fullContent);
                    tokenCountRef.current += delta.content.split(/\s+/).length;
                  }
                  
                  if (delta?.['function_call']) {
                    functionCall ??= { name: '', arguments: '' };
                    if (delta['function_call'].name) {
                      functionCall.name = delta['function_call'].name;
                    }
                    if (delta['function_call'].arguments) {
                      functionCall.arguments += delta['function_call'].arguments;
                    }
                  }
                  
                  if (delta?.['tool_calls']) {
                    for (const toolCall of delta['tool_calls']) {
                      toolCalls[toolCall.index] ??= {
                        id: toolCall.id,
                        type: 'function',
                        function: { name: '', arguments: '' },
                      };
                      if (toolCall.function?.name) {
                        toolCalls[toolCall.index].function.name = toolCall.function.name;
                      }
                      if (toolCall.function?.arguments) {
                        toolCalls[toolCall.index].function.arguments += toolCall.function.arguments;
                      }
                    }
                  }
                } catch (e) {
                  console.error('Failed to parse SSE data:', e);
                }
              }
            }
          }
        } else {
          // Transform messages to match Core API expectations
          const transformedMessages = messages.map(msg => ({
            role: msg.role === 'function' ? 'assistant' as const : msg.role,
            content: msg.content,
            ...(msg.name && { name: msg.name })
          }));
          
          const result = await coreApi.chatCompletion(transformedMessages, {
            model,
            temperature: otherParams.temperature,
            maxTokens: otherParams.maxTokens,
            stream: false,
          });

          const message: ChatMessage = {
            id: uuidv4(),
            role: 'assistant',
            content: result.choices[0].message.content ?? '',
            timestamp: new Date(),
            model,
            metadata: {
              tokensUsed: result.usage?.totalTokens,
              finishReason: result.choices[0].finishReason,
            },
          };

          return message;
        }
      } catch (error: unknown) {
        if (error instanceof Error && error.name === 'AbortError') {
          return null;
        }
        
        notifications.show({
          title: 'Chat Error',
          message: (error instanceof Error ? error.message : 'Unknown error') ?? 'Failed to send message',
          color: 'red',
        });
        
        options.onStreamError?.(error instanceof Error ? error : new Error(String(error)));
        throw error;
      } finally {
        setIsStreaming(false);
        abortControllerRef.current = null;
      }
    },
    [coreApi, options]
  );

  return {
    sendMessage,
    isStreaming,
    streamingContent,
    stopStreaming,
  };
}
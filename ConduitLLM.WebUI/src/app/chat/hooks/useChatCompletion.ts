import { useState, useCallback, useRef } from 'react';
import { useCoreApi } from '@/hooks/useCoreApi';
import { ChatMessage, FunctionDefinition, ToolDefinition, ToolChoice, ChatErrorType } from '../types';
import { notifications } from '@mantine/notifications';
import { v4 as uuidv4 } from 'uuid';

interface UseChatCompletionOptions {
  onStreamStart?: () => void;
  onStreamEnd?: (message: ChatMessage) => void;
  onStreamError?: (error: Error) => void;
  onTokensPerSecond?: (tps: number) => void;
}

// Helper function to map errors to ChatMessage error metadata
function mapErrorToMetadata(error: unknown): ChatMessage['error'] {
  let type: ChatErrorType = 'server_error';
  let code: string | undefined;
  let statusCode: number | undefined;
  let retryAfter: number | undefined;
  let suggestions: string[] | undefined;
  let technical: string | undefined;
  let recoverable = false;

  if (error instanceof Error) {
    technical = error.message;
    
    // Check for specific error patterns
    if (error.message.toLowerCase().includes('rate limit')) {
      type = 'rate_limit';
      recoverable = true;
      // Extract retry-after if present (e.g., "Rate limit exceeded. Retry after 60 seconds")
      const retryMatch = error.message.match(/retry after (\d+)/i);
      if (retryMatch) {
        retryAfter = parseInt(retryMatch[1], 10);
      }
    } else if (error.message.toLowerCase().includes('authentication') || error.message.toLowerCase().includes('unauthorized')) {
      type = 'auth_error';
      suggestions = ['Check your API key configuration', 'Verify your credentials'];
    } else if (error.message.toLowerCase().includes('model') && (error.message.includes('not found') || error.message.includes('404'))) {
      type = 'model_not_found';
      suggestions = ['Try a different model', 'Check model availability'];
    } else if (error.message.toLowerCase().includes('network') || error.name === 'NetworkError' || error.name === 'AbortError') {
      type = 'network_error';
      recoverable = true;
      suggestions = ['Check your internet connection', 'Retry the request'];
    }
  }

  // Try to extract status code from fetch errors
  if (error && typeof error === 'object' && 'status' in error) {
    const errorWithStatus = error as { status: number };
    statusCode = errorWithStatus.status;
    
    // Map status codes to error types
    if (statusCode === 429) {
      type = 'rate_limit';
      recoverable = true;
    } else if (statusCode === 401 || statusCode === 403) {
      type = 'auth_error';
    } else if (statusCode === 404) {
      type = 'model_not_found';
    }
  }

  return {
    type,
    code,
    statusCode,
    retryAfter,
    suggestions,
    technical,
    recoverable,
  };
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
            const errorData = await response.json() as { error?: string; code?: string; statusCode?: number };
            const error = new Error(errorData.error ?? 'Failed to send message');
            const errorWithMeta = error as Error & { status: number; code?: string };
            errorWithMeta.status = response.status;
            errorWithMeta.code = errorData.code;
            throw errorWithMeta;
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
                  console.warn('Failed to parse SSE data:', e);
                  // Check if this is an error message from the server
                  if (data.includes('error')) {
                    try {
                      const errorData = JSON.parse(data) as { error?: string; code?: string; statusCode?: number };
                      if (errorData.error) {
                        const error = new Error(errorData.error);
                        const errorWithMeta = error as Error & { code?: string; statusCode?: number };
                        errorWithMeta.code = errorData.code;
                        errorWithMeta.statusCode = errorData.statusCode;
                        throw errorWithMeta;
                      }
                    } catch {
                      // Ignore if not a valid error object
                    }
                  }
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
        
        // Create error message with metadata
        const errorMetadata = mapErrorToMetadata(error);
        const errorMessage: ChatMessage = {
          id: uuidv4(),
          role: 'assistant',
          content: `Error: ${errorMetadata?.technical ?? 'An unexpected error occurred'}`,
          timestamp: new Date(),
          model,
          error: errorMetadata,
        };
        
        notifications.show({
          title: 'Chat Error',
          message: errorMetadata?.technical ?? 'Failed to send message',
          color: 'red',
        });
        
        options.onStreamError?.(error instanceof Error ? error : new Error(String(error)));
        
        // Return the error message instead of throwing
        return errorMessage;
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
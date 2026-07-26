import { useCallback, useRef, useMemo } from 'react';
import { v4 as uuidv4 } from 'uuid';
import {
  createToastErrorHandler,
  type ChatAttachment,
  type StreamingCallbacks,
  type StreamMessageOptions,
  type RetryInfo
} from '@/lib/gateway-api';
import type { FunctionConfigurationDto } from '@/lib/admin-api';
import { GatewayChatStreamingAdapter } from '@/lib/client/gatewayChatStreamingAdapter';
import {
  ChatParameters,
  ChatMessage,
  ChatErrorType
} from '../types';
// Needs raw notifications API: .show is passed to the transport error handler,
// .hide is used for dismissing retry notifications, and custom options (id, loading, autoClose,
// withCloseButton) are used for retry notifications that notify doesn't support.
import { notifications } from '@mantine/notifications';

interface ChatStreamingLogicParams {
  selectedModel: string | null;
  messages: ChatMessage[];
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>;
  isLoading: boolean;
  setIsLoading: (value: boolean) => void;
  setStreamingContent: (value: string | ((prev: string) => string)) => void;
  setStreamingChannel?: (value: string | null) => void;
  setTokensPerSecond: (value: number | null) => void;
  setError: (error: Error | null) => void;
  getActiveSession: () => { parameters?: Partial<ChatParameters> } | null;
  performanceSettings: {
    trackPerformanceMetrics: boolean;
    showTokensPerSecond: boolean;
    useServerMetrics: boolean;
  };
  dynamicParameters?: Record<string, unknown>;
  sendHistoryEnabled?: boolean;
  functionConfigurationIds?: number[];
  availableFunctions?: FunctionConfigurationDto[];
}

export function useChatStreamingLogic({
  selectedModel,
  messages,
  setMessages,
  isLoading,
  setIsLoading,
  setStreamingContent,
  setStreamingChannel,
  setTokensPerSecond,
  setError,
  getActiveSession,
  performanceSettings,
  dynamicParameters = {},
  sendHistoryEnabled = true,
  functionConfigurationIds,
  availableFunctions = [],
}: ChatStreamingLogicParams) {
  const streamingAdapterRef = useRef<GatewayChatStreamingAdapter | null>(null);
  
  // Create error handler with toast notifications
  const handleError = createToastErrorHandler(notifications.show);

  // Create the Gateway streaming adapter.
  const streamingAdapter = useMemo(() => {
    return new GatewayChatStreamingAdapter({
      timeoutMs: 300000, // 5 minutes
      trackPerformanceMetrics: performanceSettings.trackPerformanceMetrics,
      showTokensPerSecond: performanceSettings.showTokensPerSecond,
      useServerMetrics: performanceSettings.useServerMetrics,
      enableLogging: process.env.NODE_ENV === 'development'
    });
  }, [performanceSettings]);

  // Store reference for cleanup
  if (streamingAdapterRef.current !== streamingAdapter) {
    // Dispose old adapter
    if (streamingAdapterRef.current) {
      streamingAdapterRef.current.dispose();
    }
    streamingAdapterRef.current = streamingAdapter;
  }

  const sendMessage = useCallback(async (inputMessage: string, attachments?: ChatAttachment[]) => {
    if (!inputMessage.trim() && (!attachments || attachments.length === 0)) return;
    if (!selectedModel || isLoading) return;

    // Get session parameters early (needed for both metadata and streaming options)
    const activeSession = getActiveSession();
    const sessionParams = activeSession?.parameters ?? {} as Partial<ChatParameters>;

    // Build conversation history for API request metadata
    const allMessages = [...messages, {
      id: uuidv4(),
      role: 'user' as const,
      content: inputMessage.trim(),
      attachments,
      timestamp: new Date()
    }];
    const conversationHistory = allMessages
      .filter(m => m.role !== 'function') // Filter out function messages for API
      .map(m => ({
        role: m.role as 'user' | 'assistant',
        content: m.content,
        attachments: m.attachments ?? m.images
      }));

    // Build the API request object that will be sent
    const apiRequestData = {
      messages: [
        ...(sessionParams.systemPrompt ? [{ role: 'system' as const, content: sessionParams.systemPrompt }] : []),
        ...(sendHistoryEnabled ? conversationHistory.slice(0, -1) : []), // Include history or send empty array
        {
          role: 'user' as const,
          content: inputMessage.trim(),
          attachments
        }
      ],
      model: selectedModel,
      temperature: sessionParams.temperature,
      max_tokens: sessionParams.maxTokens,
      top_p: sessionParams.topP,
      frequency_penalty: sessionParams.frequencyPenalty,
      presence_penalty: sessionParams.presencePenalty,
      seed: sessionParams.seed,
      stop: sessionParams.stop && sessionParams.stop.length > 0 ? sessionParams.stop : undefined,
      response_format: sessionParams.responseFormat === 'json_object' ? { type: 'json_object' } : undefined,
      stream: true,
      function_configuration_ids: functionConfigurationIds,
      ...dynamicParameters
    };

    // Build function metadata if functions are selected
    const functionMetadata = functionConfigurationIds && functionConfigurationIds.length > 0 ? {
      functionIds: functionConfigurationIds,
      functionNames: availableFunctions
        .filter(f => functionConfigurationIds.includes(f.id))
        .map(f => f.configurationName)
    } : undefined;

    const userMessage: ChatMessage = {
      id: uuidv4(),
      role: 'user',
      content: inputMessage.trim(),
      attachments,
      timestamp: new Date(),
      metadata: {
        ...functionMetadata,
        apiRequest: apiRequestData
      }
    };

    setMessages(prev => [...prev, userMessage]);
    setIsLoading(true);
    setStreamingContent('');
    setTokensPerSecond(null);
    setError(null);

    // Track reasoning and tool executions during streaming
    let totalReasoning = '';
    const toolExecutions: Array<{
      tool_call_id?: string;
      function_name: string;
      status: 'started' | 'completed' | 'failed';
      result?: unknown;
      cost?: number;
      error_message?: string;
      timestamp: number;
    }> = [];

    try {

      // Prepare streaming options
      const streamingOptions: StreamMessageOptions = {
        model: selectedModel,
        stream: true,
        messages: sendHistoryEnabled ? conversationHistory.slice(0, -1) : [], // Include history or send empty array
        attachments,
        systemPrompt: sessionParams.systemPrompt,
        temperature: sessionParams.temperature,
        maxTokens: sessionParams.maxTokens,
        topP: sessionParams.topP,
        frequencyPenalty: sessionParams.frequencyPenalty,
        presencePenalty: sessionParams.presencePenalty,
        seed: sessionParams.seed,
        stop: sessionParams.stop && sessionParams.stop.length > 0 ? sessionParams.stop : undefined,
        responseFormat: sessionParams.responseFormat === 'json_object' ? 'json_object' : undefined,
        functionConfigurationIds: functionConfigurationIds,
        dynamicParameters
      };

      // Create streaming callbacks
      const callbacks: StreamingCallbacks = {
        onStart: () => {
          // Dismiss retry notification if shown (retry succeeded)
          notifications.hide('chat-retry');
          if (process.env.NODE_ENV === 'development') {
            console.warn('Chat streaming started');
          }
        },
        onChunk: (chunk) => {
          // Track the channel for visual treatment
          const channel = chunk.choices?.[0]?.delta?.channel;
          if (setStreamingChannel && channel !== undefined) {
            setStreamingChannel(channel);
          }
        },
        onContent: (content, totalContent) => {
          setStreamingContent(totalContent);
        },
        onReasoning: (reasoning, totalReasoningText) => {
          totalReasoning = totalReasoningText;
          if (process.env.NODE_ENV === 'development') {
            console.warn('[Reasoning]', reasoning);
          }
        },
        onToolExecuting: (event) => {
          // Track tool execution events
          if (event.status === 'started') {
            toolExecutions.push({
              tool_call_id: event.tool_call_id,
              function_name: event.function_name ?? 'Unknown Function',
              status: 'started',
              timestamp: Date.now()
            });
            if (process.env.NODE_ENV === 'development') {
              console.warn(`[Tool Executing] ${event.function_name} - ${event.status}`);
            }
          } else if (event.status === 'completed') {
            // Update existing entry or add new one
            const existing = toolExecutions.find(
              t => t.tool_call_id === event.tool_call_id && t.status === 'started'
            );
            if (existing) {
              existing.status = 'completed';
              existing.result = event.result;
              existing.cost = event.cost;
            } else {
              toolExecutions.push({
                tool_call_id: event.tool_call_id,
                function_name: event.function_name ?? 'Unknown Function',
                status: 'completed',
                result: event.result,
                cost: event.cost,
                timestamp: Date.now()
              });
            }
            if (process.env.NODE_ENV === 'development') {
              console.warn(`[Tool Executing] ${event.function_name} - ${event.status}`, event.result);
            }
          } else if (event.status === 'failed') {
            // Update existing entry or add new one
            const existing = toolExecutions.find(
              t => t.tool_call_id === event.tool_call_id && t.status === 'started'
            );
            if (existing) {
              existing.status = 'failed';
              existing.error_message = event.error_message;
            } else {
              toolExecutions.push({
                tool_call_id: event.tool_call_id,
                function_name: event.function_name ?? 'Unknown Function',
                status: 'failed',
                error_message: event.error_message,
                timestamp: Date.now()
              });
            }
            if (process.env.NODE_ENV === 'development') {
              console.warn(`[Tool Executing] ${event.function_name} - ${event.status}`, event.error_message);
            }
          }
        },
        onTokensPerSecond: performanceSettings.showTokensPerSecond ? (tps) => {
          setTokensPerSecond(tps);
        } : undefined,
        onComplete: ({ content, metadata }) => {
          // Note: For models like gpt-oss-120b, we may only receive reasoning content
          // which is still valid content that should be displayed
          let finalContent = content;
          if (!finalContent || finalContent.length === 0) {
            // Still create a message even if empty to show something happened
            finalContent = '[No response received]';
          }

          const assistantMessage: ChatMessage = {
            id: uuidv4(),
            role: 'assistant',
            content: finalContent,
            timestamp: new Date(),
            metadata: {
              ...metadata,
              toolCalls: undefined, // Remove from metadata as we're adding to message root
              hasReasoning: totalReasoning.length > 0,
              reasoning: totalReasoning.length > 0 ? totalReasoning : undefined,
              toolExecutions: toolExecutions.length > 0 ? toolExecutions : undefined
            } as ChatMessage['metadata'],
            toolCalls: metadata?.toolCalls // Add tool calls to message root for UI display
          };

          setMessages(prev => [...prev, assistantMessage]);
          // Clear streaming state after message is added
          setStreamingContent('');
          setStreamingChannel?.(null);
          setTokensPerSecond(null);
          setIsLoading(false);
        },
        onError: (error) => {
          console.error('Streaming error:', error);
          handleError(error, 'chat streaming');
          setError(error);

          // Map StreamingError to ChatErrorType
          let errorType: ChatErrorType = 'server_error';
          if (error.status === 429 || error.code === 'rate_limit_exceeded') {
            errorType = 'rate_limit';
          } else if (error.status === 401 || error.status === 403) {
            errorType = 'auth_error';
          } else if (error.status !== undefined && error.status >= 400 && error.status < 500) {
            errorType = 'model_not_found';
          } else if (error.code === 'ECONNREFUSED' || error.code === 'ETIMEDOUT') {
            errorType = 'network_error';
          }

          const errorMessage: ChatMessage = {
            id: uuidv4(),
            role: 'assistant',
            content: error.context ?? 'An error occurred during streaming.',
            timestamp: new Date(),
            error: {
              type: errorType,
              code: error.code,
              statusCode: error.status,
              technical: error.message,
              recoverable: error.retryable ?? false
            }
          };

          setMessages(prev => [...prev, errorMessage]);

          // Clear streaming state
          setStreamingContent('');
          setStreamingChannel?.(null);
          setTokensPerSecond(null);
          setIsLoading(false);
        },
        onRetrying: (info: RetryInfo) => {
          // Show retry notification
          notifications.show({
            id: 'chat-retry',
            title: `Retrying... (${info.attempt}/${info.maxAttempts})`,
            message: `${info.error.context ?? 'Connection issue'}. Retrying in ${Math.ceil(info.delayMs / 1000)}s`,
            loading: true,
            autoClose: info.delayMs + 500, // Close slightly after retry starts
            withCloseButton: false,
          });

          if (process.env.NODE_ENV === 'development') {
            console.warn(`[Chat Retry] Attempt ${info.attempt}/${info.maxAttempts}, delay ${info.delayMs}ms:`, info.error.message);
          }
        },
        onAbort: () => {
          if (process.env.NODE_ENV === 'development') {
            console.warn('Chat streaming aborted');
          }
          // Clean up state when aborted
          setStreamingContent('');
          setStreamingChannel?.(null);
          setTokensPerSecond(null);
          setIsLoading(false);
        }
      };

      // Use the streaming adapter
      await streamingAdapter.streamMessage(inputMessage.trim(), streamingOptions, callbacks);
      
    } catch (err) {
      console.error('Chat error:', err);
      handleError(err, 'chat');
      setError(err as Error);
    } finally {
      // Cleanup handled in callbacks
      setTokensPerSecond(null);
    }
  }, [selectedModel, messages, isLoading, getActiveSession, performanceSettings, handleError, setMessages, setIsLoading, setStreamingContent, setStreamingChannel, setTokensPerSecond, setError, dynamicParameters, streamingAdapter, sendHistoryEnabled, functionConfigurationIds, availableFunctions]);

  const abortMessage = useCallback(() => {
    if (streamingAdapterRef.current) {
      streamingAdapterRef.current.abort();
    }
  }, []);

  return {
    sendMessage,
    abortMessage,
    // Provide a ref-like interface for compatibility
    abortControllerRef: {
      current: streamingAdapterRef.current ? { abort: abortMessage } : null
    }
  };
}


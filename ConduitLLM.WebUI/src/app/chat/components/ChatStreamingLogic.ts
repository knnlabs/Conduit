import { useCallback, useRef } from 'react';
import { v4 as uuidv4 } from 'uuid';
import { 
  createToastErrorHandler,
  shouldShowBalanceWarning,
  parseSSEStream,
  buildMessageContent,
  type ImageAttachment,
  SSEEventType
} from '@knn_labs/conduit-core-client';
import { 
  ChatParameters, 
  ChatMessage,
  ChatCompletionRequest,
  type MessageContent
} from '../types';
import { StreamingPerformanceMetrics, UsageData, MetricsEventData } from '../types/metrics';
import { notifications } from '@mantine/notifications';

interface ChatStreamingLogicParams {
  selectedModel: string | null;
  messages: ChatMessage[];
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>;
  isLoading: boolean;
  setIsLoading: (value: boolean) => void;
  setStreamingContent: (value: string | ((prev: string) => string)) => void;
  setTokensPerSecond: (value: number | null) => void;
  setError: (error: Error | null) => void;
  getActiveSession: () => { parameters?: Partial<ChatParameters> } | null;
  performanceSettings: {
    trackPerformanceMetrics: boolean;
    showTokensPerSecond: boolean;
    useServerMetrics: boolean;
  };
  dynamicParameters?: Record<string, unknown>;
}

export function useChatStreamingLogic({
  selectedModel,
  messages,
  setMessages,
  isLoading,
  setIsLoading,
  setStreamingContent,
  setTokensPerSecond,
  setError,
  getActiveSession,
  performanceSettings,
  dynamicParameters = {},
}: ChatStreamingLogicParams) {
  const abortControllerRef = useRef<AbortController | null>(null);
  
  // Create error handler with toast notifications
  const handleError = createToastErrorHandler(notifications.show);

  const sendMessage = useCallback(async (inputMessage: string, images?: ImageAttachment[]) => {
    if (!inputMessage.trim() && (!images || images.length === 0)) return;
    if (!selectedModel || isLoading) return;

    const userMessage: ChatMessage = {
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
    const startTime = Date.now();

    try {
      // Convert message format for API
      
      // Get session parameters
      const activeSession = getActiveSession();
      const sessionParams = activeSession?.parameters ?? {} as Partial<ChatParameters>;
      
      // Build messages array with optional system prompt
      const allMessages = [...messages, userMessage];
      const apiMessages: Array<{ role: 'system' | 'user' | 'assistant'; content: MessageContent }> = allMessages
        .filter(m => m.role !== 'function') // Filter out function messages for API
        .map(m => ({
          role: m.role as 'system' | 'user' | 'assistant',
          content: m.images && m.images.length > 0 
            ? buildMessageContent(m.content, m.images)
            : m.content
        }));
      
      // Prepend system prompt if it exists
      if (sessionParams.systemPrompt) {
        apiMessages.unshift({ role: 'system' as const, content: sessionParams.systemPrompt });
      }
      
      const requestBody: ChatCompletionRequest = {
        messages: apiMessages,
        model: selectedModel,
        stream: sessionParams.stream ?? true,
        // Include all session parameters
        temperature: sessionParams.temperature,
        max_tokens: sessionParams.maxTokens,
        top_p: sessionParams.topP,
        frequency_penalty: sessionParams.frequencyPenalty,
        presence_penalty: sessionParams.presencePenalty,
        ...(sessionParams.seed !== undefined && { seed: sessionParams.seed }),
        ...(sessionParams.stop && sessionParams.stop.length > 0 && { stop: sessionParams.stop }),
        ...(sessionParams.responseFormat === 'json_object' && { 
          response_format: { type: 'json_object' } 
        }),
        // Include dynamic parameters from UI
        ...dynamicParameters
      };
      
      // Request logged for debugging
      if (process.env.NODE_ENV === 'development') {
        console.warn('Sending chat request:', {
          model: selectedModel,
          messageCount: requestBody.messages.length,
          lastMessage: requestBody.messages[requestBody.messages.length - 1],
          dynamicParameters: dynamicParameters,
          fullRequest: requestBody
        });
      }
      
      // Cancel any previous request
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      
      // Create new abort controller for this request
      const controller = new AbortController();
      abortControllerRef.current = controller;
      
      // Set a generous timeout for streaming responses (5 minutes)
      const timeoutId = setTimeout(() => {
        controller.abort();
        console.warn('Request timed out after 5 minutes');
      }, 300000);
      
      // Use the SDK through our API route (consistent with images/videos)
      const response = await fetch('/api/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
        signal: controller.signal,
      });
      
      if (process.env.NODE_ENV === 'development') {
        console.warn('Using SDK-backed chat completions API');
      }
      
      // Clear the timeout once we get a response
      clearTimeout(timeoutId);

      if (!response.ok) {
        // This will throw the appropriate ConduitError subclass (including InsufficientBalanceError for 402)
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      // Handle response based on streaming mode
      if (sessionParams.stream ?? true) {
        await handleStreamingResponse(response, startTime, setMessages, setStreamingContent, setTokensPerSecond, setError, performanceSettings, selectedModel, handleError);
      } else {
        await handleNonStreamingResponse(response, startTime, setMessages, performanceSettings, selectedModel);
      }
    } catch (err) {
      await handleSendMessageError(err, setError, setMessages, handleError);
    } finally {
      setIsLoading(false);
      setStreamingContent('');
      setTokensPerSecond(null);
      // Clean up abort controller reference
      if (abortControllerRef.current) {
        abortControllerRef.current = null;
      }
    }
  }, [selectedModel, messages, isLoading, getActiveSession, performanceSettings, handleError, setMessages, setIsLoading, setStreamingContent, setTokensPerSecond, setError, dynamicParameters]);

  return {
    sendMessage,
    abortControllerRef,
  };
}

async function handleStreamingResponse(
  response: Response,
  startTime: number,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>,
  setStreamingContent: (value: string | ((prev: string) => string)) => void,
  setTokensPerSecond: (value: number | null) => void,
  setError: (error: Error | null) => void,
  performanceSettings: { trackPerformanceMetrics: boolean; showTokensPerSecond: boolean; useServerMetrics: boolean },
  selectedModel: string,
  handleError: (error: unknown, context: string) => void
) {
  // Handle streaming response
  const reader = response.body?.getReader();
  if (!reader) throw new Error('No response body reader');
  
  let fullContent = '';
  const finalMetrics: Partial<StreamingPerformanceMetrics & UsageData> = {};

  // Process SSE stream with proper event parsing
  for await (const event of parseSSEStream(reader)) {
    if (event.data === '[DONE]') {
      const endTime = Date.now();
      const duration = (endTime - startTime) / 1000;
      
      // Calculate final metrics with proper fallbacks
      if (process.env.NODE_ENV === 'development') {
        console.warn('Final metrics collected:', finalMetrics);
      }
      const totalTokens = finalMetrics.total_tokens ?? finalMetrics.completion_tokens ?? 0;
      // Use completion_tokens_per_second for more accurate generation speed
      const tokensPerSec = (finalMetrics as StreamingPerformanceMetrics).completion_tokens_per_second 
        ?? (finalMetrics as StreamingPerformanceMetrics).tokens_per_second 
        ?? (totalTokens > 0 && duration > 0 ? totalTokens / duration : 0);
      const latencyMs = (finalMetrics as MetricsEventData).total_latency_ms ?? duration * 1000;
      
      const metadata = performanceSettings.trackPerformanceMetrics
        ? {
            tokensUsed: totalTokens,
            tokensPerSecond: tokensPerSec,
            latency: latencyMs,
            provider: finalMetrics.provider,
            model: finalMetrics.model ?? selectedModel,
            promptTokens: finalMetrics.prompt_tokens,
            completionTokens: finalMetrics.completion_tokens,
            timeToFirstToken: (finalMetrics as StreamingPerformanceMetrics).time_to_first_token_ms,
            streaming: true,
          }
        : { streaming: true };
      
      const assistantMessage: ChatMessage = {
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
      case SSEEventType.Content: {
        // Handle content chunks
        const contentData = event.data as { choices?: Array<{ delta?: { content?: string } }> };
        const delta = contentData?.choices?.[0]?.delta;
        
        if (delta?.content) {
          fullContent += delta.content;
          // Update streaming content incrementally for progressive rendering
          setStreamingContent(prev => prev + delta.content);
        }

        // Check for inline performance metrics (backward compatibility)
        const performanceData = event.data as { performance?: StreamingPerformanceMetrics };
        if (performanceData?.performance && performanceSettings.useServerMetrics) {
          Object.assign(finalMetrics, performanceData.performance);
          // Use completion_tokens_per_second for more accurate generation speed
          const tps = performanceData.performance.completion_tokens_per_second 
            ?? performanceData.performance.tokens_per_second;
          if (tps && performanceSettings.showTokensPerSecond) {
            setTokensPerSecond(tps);
          }
        }
        
        // Check for usage data (OpenAI style)
        const usageData = event.data as { usage?: UsageData };
        if (usageData?.usage) {
          Object.assign(finalMetrics, usageData.usage);
        }
        break;
      }
        
      case SSEEventType.Metrics: {
        // Handle live metrics updates
        if (performanceSettings.useServerMetrics) {
          const metricsData = event.data as MetricsEventData;
          if (process.env.NODE_ENV === 'development') {
            console.warn('Metrics event received:', metricsData);
          }
          Object.assign(finalMetrics, metricsData);
          // Use completion_tokens_per_second for more accurate generation speed
          const tps = metricsData.completion_tokens_per_second ?? metricsData.tokens_per_second;
          if (tps !== undefined && performanceSettings.showTokensPerSecond) {
            setTokensPerSecond(tps);
          }
        }
        break;
      }
        
      case SSEEventType.MetricsFinal: {
        // Handle final metrics
        if (performanceSettings.useServerMetrics) {
          const finalMetricsData = event.data as MetricsEventData;
          if (process.env.NODE_ENV === 'development') {
            console.warn('Final metrics event received:', finalMetricsData);
          }
          Object.assign(finalMetrics, finalMetricsData);
        }
        break;
      }
        
      case SSEEventType.Error: {
        // Handle error events
        console.warn('Received SSE error event:', event);
        const errorData = event.data as { error?: string; message?: string; statusCode?: number };
        console.warn('Parsed error data:', errorData);
        const rawError = errorData.error ?? errorData.message ?? 'Unknown error occurred';
        const streamError = new Error(`Stream error: ${rawError}`);
        
        // Add status code if available
        if (errorData.statusCode) {
          (streamError as Error & { status?: number }).status = errorData.statusCode;
        }
        
        // Use enhanced error handler for notifications
        handleError(streamError, 'process streaming response');
        setError(streamError);
        
        // Stop processing on error
        setStreamingContent('');
        return;
      }
    }
  }
}

async function handleNonStreamingResponse(
  response: Response,
  startTime: number,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>,
  performanceSettings: { trackPerformanceMetrics: boolean },
  selectedModel: string
) {
  // Handle non-streaming response
  const data = await response.json() as { choices?: Array<{ message?: { content?: string } }>; usage?: UsageData; model?: string };
  const assistantContent = data.choices?.[0]?.message?.content ?? '';
  
  const requestDuration = (Date.now() - startTime) / 1000;
  // Type assertion needed for response format compatibility
  const usage = data.usage as unknown as UsageData | undefined;
  const totalTokens = usage?.total_tokens ?? 0;
  
  const metadata = performanceSettings.trackPerformanceMetrics && data.usage
    ? {
        tokensUsed: totalTokens,
        tokensPerSecond: totalTokens > 0 && requestDuration > 0 ? totalTokens / requestDuration : 0,
        latency: requestDuration * 1000,
        model: data.model ?? selectedModel,
        promptTokens: usage?.prompt_tokens,
        completionTokens: usage?.completion_tokens,
        streaming: false,
      }
    : { streaming: false };
  
  const assistantMessage: ChatMessage = {
    id: uuidv4(),
    role: 'assistant',
    content: assistantContent,
    timestamp: new Date(),
    metadata
  };
  
  setMessages(prev => [...prev, assistantMessage]);
}

async function handleSendMessageError(
  err: unknown,
  setError: (error: Error | null) => void,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>,
  handleError: (error: unknown, context: string) => void
) {
  // Check if the error is due to an abort (timeout or user cancellation)
  if (err instanceof Error && err.name === 'AbortError') {
    const errorMessage = 'Request timed out. The response took too long to generate. Please try a shorter prompt or simpler request.';
    
    const abortError = new Error(errorMessage);
    setError(abortError);
    
    // Show error in chat
    const errorMsg: ChatMessage = {
      id: uuidv4(),
      role: 'assistant',
      content: errorMessage,
      timestamp: new Date(),
      metadata: {
        tokensUsed: 0,
        tokensPerSecond: 0,
        latency: 0,
      }
    };
    setMessages(prev => [...prev, errorMsg]);
    return; // Early return for abort errors
  }
  
  // Use the enhanced error handler from SDK for notifications
  handleError(err, 'send chat message');
  const errorInstance = err instanceof Error ? err : new Error(String(err));
  setError(errorInstance);
  
  // Show error as a system message in chat
  const errorMsg: ChatMessage = {
    id: uuidv4(),
    role: 'assistant',
    content: `Error: ${errorInstance.message}`,
    timestamp: new Date(),
    metadata: {
      tokensUsed: 0,
      tokensPerSecond: 0,
      latency: 0,
    }
  };
  setMessages(prev => [...prev, errorMsg]);
  
  // Show special handling for balance errors
  if (shouldShowBalanceWarning(err)) {
    // Clear the loading state faster for balance errors since user needs to take action
    const balanceError = new Error('Please add credits to your account to continue chatting.');
    balanceError.name = 'InsufficientBalanceError';
    (balanceError as Error & { status?: number }).status = 402;
    setError(balanceError);
  }
}
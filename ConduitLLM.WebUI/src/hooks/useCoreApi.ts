'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
// Note: Using local interfaces instead of SDK types due to compatibility issues

interface ImageGenerationRequest {
  prompt: string;
  model?: string;
  size?: string;
  quality?: string;
  n?: number;
}

interface VideoGenerationRequest {
  prompt: string;
  model?: string;
  duration?: number;
  resolution?: string;
}


interface ImageGenerationResponse {
  id: string;
  url: string;
  prompt: string;
  model: string;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  createdAt: string;
  metadata?: Record<string, unknown>;
}

interface VideoGenerationResponse {
  id: string;
  url?: string;
  prompt: string;
  model: string;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  progress?: number;
  createdAt: string;
  metadata?: Record<string, unknown>;
}


interface ChatMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

interface ChatCompletionOptions {
  model?: string;
  temperature?: number;
  maxTokens?: number;
  stream?: boolean;
}

interface ChatCompletionResponse {
  id: string;
  choices: Array<{
    index: number;
    message: ChatMessage;
    finishReason?: string;
  }>;
  usage: {
    promptTokens: number;
    completionTokens: number;
    totalTokens: number;
  };
  model: string;
}

export function useCoreApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const generateImage = useCallback(async (data: ImageGenerationRequest): Promise<ImageGenerationResponse> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/images/generate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const errorData = await response.json() as { error?: string };
        throw new Error(errorData.error ?? 'Image generation failed');
      }

      const result = await response.json() as ImageGenerationResponse;

      notifications.show({
        title: 'Success',
        message: 'Image generated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Image generation failed';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const generateVideo = useCallback(async (data: VideoGenerationRequest): Promise<VideoGenerationResponse> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/videos/generate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const errorData = await response.json() as { error?: string };
        throw new Error(errorData.error ?? 'Video generation failed');
      }

      const result = await response.json() as VideoGenerationResponse;

      notifications.show({
        title: 'Success',
        message: 'Video generation started',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Video generation failed';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);



  const chatCompletion = useCallback(async (messages: ChatMessage[], options?: ChatCompletionOptions): Promise<ChatCompletionResponse> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          messages,
          ...options,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json() as { error?: string };
        throw new Error(errorData.error ?? 'Chat completion failed');
      }

      const result = await response.json() as ChatCompletionResponse;
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Chat completion failed';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    generateImage,
    generateVideo,
    chatCompletion,
    isLoading,
    error,
  };
}
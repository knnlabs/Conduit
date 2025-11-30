'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { getBrowserCoreClient } from '@/lib/client/browserCoreClient';
import type {
  ImageGenerationRequest,
  ImageGenerationResponse,
  AsyncVideoGenerationRequest,
  AsyncVideoGenerationResponse,
  ChatCompletionRequest,
  ChatCompletionResponse
} from '@knn_labs/conduit-gateway-client';

export function useCoreApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const generateImage = useCallback(async (data: ImageGenerationRequest): Promise<ImageGenerationResponse> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const client = await getBrowserCoreClient();
      const result = await client.images.generate(data);

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

  const generateVideo = useCallback(async (data: AsyncVideoGenerationRequest): Promise<AsyncVideoGenerationResponse> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const client = await getBrowserCoreClient();
      const result = await client.videos.generateAsync(data);

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

  const chatCompletion = useCallback(async (
    request: ChatCompletionRequest
  ): Promise<ChatCompletionResponse> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const client = await getBrowserCoreClient();
      const result = await client.chat.create({
        ...request,
        stream: false
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Chat completion failed';
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

  const streamChatCompletion = useCallback(async function* (
    request: ChatCompletionRequest
  ) {
    setIsLoading(true);
    setError(null);
    
    try {
      const client = await getBrowserCoreClient();
      const stream = await client.chat.create({
        ...request,
        stream: true
      });

      for await (const chunk of stream) {
        yield chunk;
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Chat stream failed';
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

  return {
    generateImage,
    generateVideo,
    chatCompletion,
    streamChatCompletion,
    isLoading,
    error,
  };
}
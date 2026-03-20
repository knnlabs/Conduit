'use client';

import { useState, useCallback } from 'react';
import { notify } from '@/lib/notifications';
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

      notify.success('Image generated successfully');

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Image generation failed';
      setError(message);
      notify.error(message);
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

      notify.success('Video generation started');

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Video generation failed';
      setError(message);
      notify.error(message);
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
      notify.error(message);
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
      notify.error(message);
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
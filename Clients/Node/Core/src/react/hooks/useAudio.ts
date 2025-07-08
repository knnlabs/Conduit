import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduit } from '../ConduitProvider';
import type { 
  AudioTranscriptionRequest,
  AudioTranscriptionResponse,
  AudioTranslationRequest,
  AudioTranslationResponse,
  TextToSpeechRequest as AudioSpeechRequest,
  TextToSpeechResponse
} from '../../models/audio';

export interface UseAudioTranscriptionOptions 
  extends Omit<
    UseMutationOptions<AudioTranscriptionResponse, Error, AudioTranscriptionRequest>,
    'mutationFn'
  > {}

export interface UseAudioTranslationOptions 
  extends Omit<
    UseMutationOptions<AudioTranslationResponse, Error, AudioTranslationRequest>,
    'mutationFn'
  > {}

export interface UseAudioSpeechOptions 
  extends Omit<
    UseMutationOptions<TextToSpeechResponse, Error, AudioSpeechRequest>,
    'mutationFn'
  > {}

export function useAudioTranscription(options?: UseAudioTranscriptionOptions) {
  const { client } = useConduit();

  return useMutation({
    mutationFn: async (request: AudioTranscriptionRequest) => {
      return await client.audio.transcribe(request);
    },
    ...options,
  });
}

export function useAudioTranslation(options?: UseAudioTranslationOptions) {
  const { client } = useConduit();

  return useMutation({
    mutationFn: async (request: AudioTranslationRequest) => {
      return await client.audio.translate(request);
    },
    ...options,
  });
}

export function useAudioSpeech(options?: UseAudioSpeechOptions) {
  const { client } = useConduit();

  return useMutation({
    mutationFn: async (request: AudioSpeechRequest) => {
      return await client.audio.generateSpeech(request);
    },
    ...options,
  });
}
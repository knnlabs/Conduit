import type { ModelDto } from '@/lib/admin-api';
import { mapDiscoveredModelCapabilities } from './modelCapabilities';

describe('mapDiscoveredModelCapabilities', () => {
  it('maps speech capabilities from model discovery data', () => {
    const capabilities = mapDiscoveredModelCapabilities({
      supportsSpeechToText: true,
      supportsTextToSpeech: true,
    } as ModelDto);

    expect(capabilities.supportsAudioTranscription).toBe(true);
    expect(capabilities.supportsTextToSpeech).toBe(true);
    expect(capabilities.supportsRealtimeAudio).toBe(false);
  });

  it('does not enable audio capabilities when model data does not support them', () => {
    const capabilities = mapDiscoveredModelCapabilities({} as ModelDto);

    expect(capabilities.supportsAudioTranscription).toBe(false);
    expect(capabilities.supportsTextToSpeech).toBe(false);
    expect(capabilities.supportsRealtimeAudio).toBe(false);
  });
});

'use client';

import { Group, Tooltip } from '@mantine/core';
import { 
  IconMessageCircle, 
  IconEye, 
  IconPhoto, 
  IconVideo, 
  IconCode,
  IconPlayerPlay,
  IconVectorBezier
} from '@tabler/icons-react';

interface Capabilities {
  supportsChat?: boolean;
  supportsVision?: boolean;
  supportsImageGeneration?: boolean;
  supportsVideoGeneration?: boolean;
  supportsAudioTranscription?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRealtimeAudio?: boolean;
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
  supportsEmbeddings?: boolean;
}

interface CapabilityIconsProps {
  capabilities: Capabilities;
  iconSize?: number;
  gap?: number;
}

const capabilityConfig = [
  { key: 'supportsChat', label: 'Chat', Icon: IconMessageCircle },
  { key: 'supportsVision', label: 'Vision', Icon: IconEye },
  { key: 'supportsImageGeneration', label: 'Image Generation', Icon: IconPhoto },
  { key: 'supportsVideoGeneration', label: 'Video Generation', Icon: IconVideo },
  { key: 'supportsFunctionCalling', label: 'Function Calling', Icon: IconCode },
  { key: 'supportsStreaming', label: 'Streaming', Icon: IconPlayerPlay },
  { key: 'supportsEmbeddings', label: 'Embeddings', Icon: IconVectorBezier },
] as const;

export function CapabilityIcons({ capabilities, iconSize = 16, gap = 4 }: CapabilityIconsProps) {
  return (
    <Group gap={gap}>
      {capabilityConfig.map(({ key, label, Icon }) => {
        const supported = capabilities[key as keyof Capabilities] === true;
        const iconProps = { 
          size: iconSize, 
          style: { opacity: supported ? 1 : 0.3 } 
        };
        
        return (
          <Tooltip key={key} label={`${label}: ${supported ? 'Supported' : 'Not supported'}`}>
            <span>
              <Icon {...iconProps} />
            </span>
          </Tooltip>
        );
      })}
    </Group>
  );
}
export function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 Bytes';

  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
}

export function formatDate(dateString: string): string {
  const date = new Date(dateString);
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

export function getMediaTypeIcon(type: 'image' | 'video'): string {
  return type === 'image' ? '🖼️' : '🎬';
}

export function getProviderColor(provider?: string): string {
  const colors: Record<string, string> = {
    OpenAI: 'blue',
    Anthropic: 'grape',
    MiniMax: 'orange',
    Replicate: 'teal',
    ElevenLabs: 'pink',
  };
  return colors[provider ?? ''] ?? 'gray';
}
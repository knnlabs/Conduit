// Number formatting utilities
export function formatNumber(num: number): string {
  return new Intl.NumberFormat('en-US').format(num);
}

export function formatPercent(num: number, decimals: number = 1): string {
  return `${num.toFixed(decimals)}%`;
}

// Currency formatting
export function formatCurrency(amount: number, currency: string = 'USD'): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency,
  }).format(amount);
}

// Byte size formatting
export function formatBytes(bytes: number, decimals: number = 2): string {
  if (bytes === 0) return '0 Bytes';

  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];

  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}

// Date and time formatting
export function formatDate(date: Date | string): string {
  const d = typeof date === 'string' ? new Date(date) : date;
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(d);
}

export function formatDateTime(date: Date | string): string {
  const d = typeof date === 'string' ? new Date(date) : date;
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

export function formatRelativeTime(date: Date | string): string {
  const d = typeof date === 'string' ? new Date(date) : date;
  const now = new Date();
  const diffInSeconds = Math.floor((now.getTime() - d.getTime()) / 1000);

  if (diffInSeconds < 60) return 'just now';
  if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`;
  if (diffInSeconds < 86400) return `${Math.floor(diffInSeconds / 3600)}h ago`;
  if (diffInSeconds < 604800) return `${Math.floor(diffInSeconds / 86400)}d ago`;
  if (diffInSeconds < 2592000) return `${Math.floor(diffInSeconds / 604800)}w ago`;
  if (diffInSeconds < 31536000) return `${Math.floor(diffInSeconds / 2592000)}mo ago`;
  return `${Math.floor(diffInSeconds / 31536000)}y ago`;
}

// Duration formatting
export function formatDuration(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = Math.floor(seconds % 60);

  if (hours > 0) {
    return `${hours}:${minutes.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }
  return `${minutes}:${secs.toString().padStart(2, '0')}`;
}

// Token/credit formatting
export function formatTokens(tokens: number): string {
  if (tokens < 1000) return tokens.toString();
  if (tokens < 1000000) return `${(tokens / 1000).toFixed(1)}K`;
  if (tokens < 1000000000) return `${(tokens / 1000000).toFixed(1)}M`;
  return `${(tokens / 1000000000).toFixed(1)}B`;
}

// Truncate text with ellipsis
export function truncateText(text: string, maxLength: number): string {
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength - 3) + '...';
}

// Format model name
export function formatModelName(model: string): string {
  // Convert model IDs to readable names
  const modelNames: Record<string, string> = {
    'gpt-4': 'GPT-4',
    'gpt-4-turbo': 'GPT-4 Turbo',
    'gpt-3.5-turbo': 'GPT-3.5 Turbo',
    'claude-3-opus': 'Claude 3 Opus',
    'claude-3-sonnet': 'Claude 3 Sonnet',
    'claude-3-haiku': 'Claude 3 Haiku',
    'dall-e-3': 'DALL·E 3',
    'dall-e-2': 'DALL·E 2',
    'whisper-1': 'Whisper',
    'tts-1': 'TTS 1',
    'tts-1-hd': 'TTS 1 HD',
  };

  return modelNames[model] || model;
}

// Format status
export function formatStatus(status: string): { label: string; color: string } {
  const statusMap: Record<string, { label: string; color: string }> = {
    active: { label: 'Active', color: 'green' },
    inactive: { label: 'Inactive', color: 'gray' },
    error: { label: 'Error', color: 'red' },
    warning: { label: 'Warning', color: 'orange' },
    pending: { label: 'Pending', color: 'blue' },
    success: { label: 'Success', color: 'green' },
    failed: { label: 'Failed', color: 'red' },
  };

  return statusMap[status.toLowerCase()] || { label: status, color: 'gray' };
}

// Format percentage with color
export function formatPercentageWithColor(value: number): { text: string; color: string } {
  const text = formatPercent(value);
  let color = 'green';
  
  if (value < 50) color = 'red';
  else if (value < 80) color = 'orange';
  else if (value < 95) color = 'yellow';
  
  return { text, color };
}

// Format latency
export function formatLatency(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

// Format rate limit
export function formatRateLimit(rpm: number): string {
  if (rpm < 1000) return `${rpm} RPM`;
  if (rpm < 1000000) return `${(rpm / 1000).toFixed(1)}K RPM`;
  return `${(rpm / 1000000).toFixed(1)}M RPM`;
}
/**
 * Media-specific display utilities.
 * For general formatting (dates, file sizes, currency), use the SDK formatters
 * via '@/lib/utils/formatters'.
 */

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

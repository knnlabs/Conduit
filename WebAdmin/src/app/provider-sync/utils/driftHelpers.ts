import type { DriftItemDto } from '@/lib/admin-api';

/** Mantine color for each drift type badge. */
export function driftTypeColor(driftType: string): string {
  switch (driftType) {
    case 'Pricing':
      return 'blue';
    case 'MissingCost':
      return 'orange';
    case 'ContextWindow':
      return 'cyan';
    case 'Capabilities':
      return 'grape';
    case 'ModelRemoved':
      return 'red';
    case 'ModelDeprecated':
      return 'yellow';
    default:
      return 'gray';
  }
}

/** Human-readable label for a drift type. */
export function driftTypeLabel(driftType: string): string {
  switch (driftType) {
    case 'MissingCost':
      return 'Missing cost';
    case 'ContextWindow':
      return 'Context window';
    case 'ModelRemoved':
      return 'Model removed';
    case 'ModelDeprecated':
      return 'Model deprecated';
    default:
      return driftType;
  }
}

/** Mantine color for each drift status badge. */
export function driftStatusColor(status: string): string {
  switch (status) {
    case 'Pending':
      return 'blue';
    case 'Applied':
      return 'green';
    case 'Dismissed':
      return 'gray';
    case 'AutoResolved':
      return 'teal';
    default:
      return 'gray';
  }
}

/** Friendly labels for the known drift payload fields; falls back to a de-camelCased key. */
const FIELD_LABELS: Record<string, string> = {
  inputPerMillion: 'Input ($/M tokens)',
  outputPerMillion: 'Output ($/M tokens)',
  cachedInputPerMillion: 'Cached input ($/M)',
  cachedWritePerMillion: 'Cache write ($/M)',
  maxInputTokens: 'Max input tokens',
  maxOutputTokens: 'Max output tokens',
  supportsVision: 'Vision',
  supportsFunctionCalling: 'Function calling',
  supportsImageGeneration: 'Image generation',
};

export function fieldLabel(key: string): string {
  if (FIELD_LABELS[key]) return FIELD_LABELS[key];
  // De-camelCase fallback: "someKey" -> "Some key"
  const spaced = key.replace(/([A-Z])/g, ' $1').trim();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

/** Render a payload value for display (booleans as Yes/No, null as an em dash). */
export function formatDriftValue(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'number') return String(value);
  if (typeof value === 'string') return value;
  // Objects/arrays — render as compact JSON rather than "[object Object]".
  return JSON.stringify(value);
}

/** Safely parse a drift payload JSON string into a keyed record. */
export function parseDriftPayload(json: string | null | undefined): Record<string, unknown> {
  if (!json) return {};
  try {
    const parsed: unknown = JSON.parse(json);
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    return {};
  } catch {
    return {};
  }
}

export interface DriftWarning {
  color: string;
  message: string;
}

/**
 * Compute review warnings for a drift item. Capability drift mutates shared model flags,
 * and zero-price proposals will zero out billing — both deserve an explicit admin warning.
 */
export function driftWarnings(item: DriftItemDto): DriftWarning[] {
  const warnings: DriftWarning[] = [];

  if (item.driftType === 'Capabilities') {
    warnings.push({
      color: 'orange',
      message:
        'Applying writes shared model capability flags — this affects every provider whose mappings route this model, not just this one.',
    });
  }

  if (item.driftType === 'Pricing' || item.driftType === 'MissingCost') {
    const proposed = parseDriftPayload(item.proposedValuesJson);
    const priceFields = ['inputPerMillion', 'outputPerMillion'];
    const hasZeroPrice = priceFields.some((f) => {
      const v = proposed[f];
      return typeof v === 'number' && v === 0;
    });
    if (hasZeroPrice) {
      warnings.push({
        color: 'red',
        message:
          'Proposed pricing is $0 (a free variant). Applying will zero out billing for this model — confirm this is intended.',
      });
    }
  }

  return warnings;
}

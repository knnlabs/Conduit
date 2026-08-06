import { normalizeProviderType, type CreateProviderTool } from '@/lib/admin-api';

export const MAX_PROVIDER_TOOL_IMPORT_ROWS = 5_000;
export const LARGE_PROVIDER_TOOL_IMPORT_ROWS = 500;

export interface ProviderToolImportRow {
  rowNumber: number;
  tool: CreateProviderTool | null;
  providerLabel: string;
  toolName: string;
  errors: string[];
}

export interface ProviderToolImportPreview {
  rows: ProviderToolImportRow[];
  validTools: CreateProviderTool[];
  duplicateCount: number;
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const isOptionalString = (value: unknown): value is string | null | undefined =>
  value === undefined || value === null || typeof value === 'string';

export function parseProviderToolImport(contents: string): ProviderToolImportPreview {
  let input: unknown;

  try {
    input = JSON.parse(contents);
  } catch {
    throw new Error('The selected file is not valid JSON.');
  }

  if (!Array.isArray(input)) {
    throw new Error('Provider tool imports must contain a JSON array.');
  }

  if (input.length === 0) {
    throw new Error('The selected file does not contain any provider tools.');
  }

  if (input.length > MAX_PROVIDER_TOOL_IMPORT_ROWS) {
    throw new Error(
      `This file contains ${input.length.toLocaleString()} records. Import at most ${MAX_PROVIDER_TOOL_IMPORT_ROWS.toLocaleString()} records at a time.`,
    );
  }

  const seenTools = new Set<string>();
  let duplicateCount = 0;

  const rows = input.map((value, index): ProviderToolImportRow => {
    const rowNumber = index + 1;
    const errors: string[] = [];

    if (!isRecord(value)) {
      return {
        rowNumber,
        tool: null,
        providerLabel: '—',
        toolName: '—',
        errors: ['Record must be a JSON object.'],
      };
    }

    const provider = value.provider;
    const rawToolName = value.toolName;
    const normalizedProvider =
      typeof provider === 'string' || typeof provider === 'number'
        ? normalizeProviderType(provider)
        : undefined;

    if (!normalizedProvider || normalizedProvider === 'unknown') {
      errors.push('provider must be a recognized provider name or legacy positive integer.');
    }

    if (typeof rawToolName !== 'string' || rawToolName.trim().length === 0) {
      errors.push('toolName must be a non-empty string.');
    }

    if (!isOptionalString(value.toolParameters)) {
      errors.push('toolParameters must be a string or null.');
    }

    if (
      value.costPerUnit !== undefined &&
      value.costPerUnit !== null &&
      (typeof value.costPerUnit !== 'number' ||
        !Number.isFinite(value.costPerUnit) ||
        value.costPerUnit < 0)
    ) {
      errors.push('costPerUnit must be a non-negative number or null.');
    }

    if (!isOptionalString(value.billingUnit)) {
      errors.push('billingUnit must be a string or null.');
    }

    if (!isOptionalString(value.costDescription)) {
      errors.push('costDescription must be a string or null.');
    }

    if (value.isActive !== undefined && typeof value.isActive !== 'boolean') {
      errors.push('isActive must be a boolean.');
    }

    const toolName = typeof rawToolName === 'string' ? rawToolName.trim() : '';
    if (normalizedProvider && normalizedProvider !== 'unknown' && toolName) {
      const duplicateKey = `${normalizedProvider}:${toolName.toLowerCase()}`;
      if (seenTools.has(duplicateKey)) {
        duplicateCount += 1;
        errors.push('Duplicate provider and toolName in this file.');
      } else {
        seenTools.add(duplicateKey);
      }
    }

    const tool: CreateProviderTool | null = errors.length === 0
      ? {
          provider: normalizedProvider ?? 'unknown',
          toolName,
          toolParameters: (value.toolParameters as string | null | undefined) ?? null,
          costPerUnit: (value.costPerUnit as number | null | undefined) ?? null,
          billingUnit: (value.billingUnit as string | null | undefined) ?? null,
          costDescription: (value.costDescription as string | null | undefined) ?? null,
          isActive: (value.isActive as boolean | undefined) ?? true,
        }
      : null;

    return {
      rowNumber,
      tool,
      providerLabel:
        typeof provider === 'string' || typeof provider === 'number' ? String(provider) : '—',
      toolName: toolName || '—',
      errors,
    };
  });

  return {
    rows,
    validTools: rows.flatMap(row => (row.tool ? [row.tool] : [])),
    duplicateCount,
  };
}

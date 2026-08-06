'use client';

import { useState, useCallback, type ReactNode } from 'react';
import { Textarea, Alert, Text, Group, Button, Stack } from '@mantine/core';
import { IconAlertCircle } from '@tabler/icons-react';

interface JsonEditorFieldProps {
  /** Label shown above the editor. */
  label?: string;
  /** Current JSON string value. */
  value: string;
  /** Called with the new value on every change. */
  onChange: (value: string) => void;
  /**
   * Called whenever validity changes, so the parent can gate submission
   * (e.g. `disabled={!jsonValid}`). Fires on every edit.
   */
  onValidityChange?: (isValid: boolean) => void;
  rows?: number;
  placeholder?: string;
  /** Optional preview, rendered only when the JSON is valid and non-empty. */
  renderPreview?: (value: string) => ReactNode;
  /** Where to render the preview relative to the textarea. Default: 'below'. */
  previewPosition?: 'above' | 'below';
  /** When true, the preview is hidden behind a Show/Hide toggle button. */
  collapsiblePreview?: boolean;
}

/**
 * Shared JSON editor: a monospace Textarea with live JSON validation, an inline
 * error, and an optional preview. Replaces the copy-pasted "Parameters (JSON)"
 * block (validate + textarea + error Alert) duplicated across entity modals.
 */
export function JsonEditorField({
  label,
  value,
  onChange,
  onValidityChange,
  rows = 8,
  placeholder = 'JSON parameters...',
  renderPreview,
  previewPosition = 'below',
  collapsiblePreview = false,
}: JsonEditorFieldProps) {
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [showPreview, setShowPreview] = useState(!collapsiblePreview);

  const handleChange = useCallback(
    (next: string) => {
      onChange(next);
      let error: string | null = null;
      if (next) {
        try {
          JSON.parse(next);
        } catch {
          error = 'Invalid JSON format';
        }
      }
      setJsonError(error);
      onValidityChange?.(error === null);
    },
    [onChange, onValidityChange]
  );

  const previewVisible =
    Boolean(renderPreview) && !jsonError && Boolean(value) && (collapsiblePreview ? showPreview : true);
  const preview = previewVisible && renderPreview ? renderPreview(value) : null;

  return (
    <Stack gap="xs">
      {(label ?? (collapsiblePreview && renderPreview)) && (
        <Group justify="space-between">
          {label && (
            <Text size="sm" fw={500}>
              {label}
            </Text>
          )}
          {collapsiblePreview && renderPreview && (
            <Button size="xs" variant="subtle" onClick={() => setShowPreview((s) => !s)}>
              {showPreview ? 'Hide' : 'Show'} Preview
            </Button>
          )}
        </Group>
      )}

      {previewPosition === 'above' && preview}

      <Textarea
        placeholder={placeholder}
        rows={rows}
        style={{ fontFamily: 'monospace' }}
        value={value}
        onChange={(e) => handleChange(e.currentTarget.value)}
        error={jsonError}
      />

      {previewPosition === 'below' && preview}

      {jsonError && (
        <Alert icon={<IconAlertCircle size={16} />} color="red" variant="light">
          {jsonError}
        </Alert>
      )}
    </Stack>
  );
}

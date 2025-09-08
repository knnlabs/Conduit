'use client';

import React, { useCallback, KeyboardEvent } from 'react';
import { Textarea, Text, Group, Stack, TextareaProps } from '@mantine/core';

/**
 * Props for the MediaPromptInput component
 */
export interface MediaPromptInputProps {
  /** Current value of the prompt */
  value: string;
  
  /** Callback when the prompt changes */
  onChange: (value: string) => void;
  
  /** Callback when submit is triggered */
  onSubmit: () => void;
  
  /** Placeholder text for the input */
  placeholder?: string;
  
  /** Whether the input is disabled */
  disabled?: boolean;
  
  /** Whether the component is in a loading state */
  isLoading?: boolean;
  
  /** Maximum character length for the prompt */
  maxLength?: number;
  
  /** Keyboard shortcut for submit */
  submitShortcut?: 'enter' | 'ctrl+enter' | 'cmd+enter' | 'ctrl+cmd+enter';
  
  /** Whether to show character count */
  showCharCount?: boolean;
  
  /** Label for the textarea */
  label?: string;
  
  /** Description text below the textarea */
  description?: string;
  
  /** Minimum number of rows */
  minRows?: number;
  
  /** Maximum number of rows */
  maxRows?: number;
  
  /** Whether textarea should autosize */
  autosize?: boolean;
  
  /** Additional Mantine Textarea props */
  textareaProps?: Partial<TextareaProps>;
  
  /** Additional content to render in the character count area */
  additionalInfo?: React.ReactNode;
  
  /** Custom character count formatter */
  formatCharCount?: (current: number, max?: number) => string;
}

/**
 * Shared prompt input component for media generation
 */
export function MediaPromptInput({
  value,
  onChange,
  onSubmit,
  placeholder = 'Enter your prompt...',
  disabled = false,
  isLoading = false,
  maxLength,
  submitShortcut = 'ctrl+enter',
  showCharCount = true,
  label,
  description,
  minRows = 4,
  maxRows = 10,
  autosize = true,
  textareaProps = {},
  additionalInfo,
  formatCharCount,
}: MediaPromptInputProps) {
  
  // Handle keyboard shortcuts
  const handleKeyDown = useCallback((e: KeyboardEvent<HTMLTextAreaElement>) => {
    const isEnter = e.key === 'Enter';
    const isCtrl = e.ctrlKey;
    const isCmd = e.metaKey;
    const isShift = e.shiftKey;
    
    // Don't submit if shift is pressed (for new lines)
    if (isShift) return;
    
    let shouldSubmit = false;
    
    switch (submitShortcut) {
      case 'enter':
        shouldSubmit = isEnter && !isCtrl && !isCmd;
        break;
      case 'ctrl+enter':
        shouldSubmit = isEnter && isCtrl && !isCmd;
        break;
      case 'cmd+enter':
        shouldSubmit = isEnter && !isCtrl && isCmd;
        break;
      case 'ctrl+cmd+enter':
        shouldSubmit = isEnter && (isCtrl || isCmd);
        break;
    }
    
    if (shouldSubmit) {
      e.preventDefault();
      if (!disabled && !isLoading && value.trim()) {
        onSubmit();
      }
    }
  }, [submitShortcut, disabled, isLoading, value, onSubmit]);
  
  // Format the shortcut description
  const getShortcutDescription = useCallback(() => {
    switch (submitShortcut) {
      case 'enter':
        return 'Press Enter to submit, Shift+Enter for new line';
      case 'ctrl+enter':
        return 'Press Ctrl+Enter to submit';
      case 'cmd+enter':
        return 'Press Cmd+Enter to submit';
      case 'ctrl+cmd+enter':
        return 'Press Ctrl/Cmd+Enter to submit';
      default:
        return '';
    }
  }, [submitShortcut]);
  
  // Format character count display
  const getCharCountDisplay = useCallback(() => {
    if (!showCharCount) return null;
    
    if (formatCharCount) {
      return formatCharCount(value.length, maxLength);
    }
    
    const charCount = `${value.length} character${value.length !== 1 ? 's' : ''}`;
    
    if (maxLength) {
      const remaining = maxLength - value.length;
      const percentage = (value.length / maxLength) * 100;
      
      let color = 'dimmed';
      if (percentage > 90) color = 'red';
      else if (percentage > 75) color = 'orange';
      else if (percentage > 50) color = 'yellow';
      
      return (
        <Text size="sm" c={color}>
          {charCount} ({remaining} remaining)
        </Text>
      );
    }
    
    // Add warning for very long prompts
    let warningText = '';
    let color = 'dimmed';
    
    if (value.length > 2000) {
      warningText = ' (extremely long)';
      color = 'red';
    } else if (value.length > 1000) {
      warningText = ' (very long)';
      color = 'orange';
    } else if (value.length > 500) {
      warningText = ' (long)';
      color = 'yellow';
    }
    
    return (
      <Text size="sm" c={color}>
        {charCount}{warningText}
      </Text>
    );
  }, [showCharCount, formatCharCount, value.length, maxLength]);
  
  // Combine description with shortcut info
  const fullDescription = description ?? getShortcutDescription();
  
  return (
    <Stack gap="xs">
      <Textarea
        label={label}
        value={value}
        onChange={(e) => onChange(e.currentTarget.value)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        disabled={disabled || isLoading}
        minRows={minRows}
        maxRows={maxRows}
        autosize={autosize}
        maxLength={maxLength}
        description={fullDescription}
        error={maxLength && value.length > maxLength ? 'Prompt exceeds maximum length' : undefined}
        {...textareaProps}
      />
      
      {(showCharCount || additionalInfo) && (
        <Group justify="space-between">
          <div>{getCharCountDisplay()}</div>
          {additionalInfo && <div>{additionalInfo}</div>}
        </Group>
      )}
    </Stack>
  );
}

/**
 * Helper hook for managing prompt state
 */
export function useMediaPrompt(initialValue = '') {
  const [value, setValue] = React.useState(initialValue);
  
  const clear = useCallback(() => setValue(''), []);
  const reset = useCallback(() => setValue(initialValue), [initialValue]);
  
  return {
    value,
    setValue,
    clear,
    reset,
  };
}
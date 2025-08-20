'use client';

import {
  TextInput,
  NumberInput,
  Switch,
  Stack,
  Group,
  Text,
} from '@mantine/core';
import { FieldGroupProps, BaseFieldProps } from './FormFieldTypes';

// Base field group component for consistent layout
export function FieldGroup({
  label,
  description,
  required = false,
  children,
  gap = 'xs',
  className,
}: FieldGroupProps) {
  return (
    <Stack gap={gap} className={className}>
      {label && (
        <Text size="sm" fw={500}>
          {label}
          {required && <Text span c="red"> *</Text>}
        </Text>
      )}
      {children}
      {description && (
        <Text size="xs" c="dimmed">
          {description}
        </Text>
      )}
    </Stack>
  );
}

// Name field component with standard validation
export interface NameFieldProps extends BaseFieldProps {
  placeholder?: string;
  minLength?: number;
  maxLength?: number;
}

export function NameField({
  form,
  fieldName,
  label = 'Name',
  placeholder,
  description,
  required = true,
  disabled = false,
}: NameFieldProps) {
  return (
    <FieldGroup
      label={label}
      description={description}
      required={required}
    >
      <TextInput
        placeholder={placeholder ?? `Enter ${label.toLowerCase()}`}
        disabled={disabled}
        {...form.getInputProps(fieldName)}
      />
    </FieldGroup>
  );
}

// Budget field component with proper number formatting
export interface BudgetFieldProps extends BaseFieldProps {
  min?: number;
  max?: number;
  precision?: number;
  currency?: string;
}

export function BudgetField({
  form,
  fieldName,
  label = 'Budget',
  description,
  required = false,
  min = 0,
  max = 10000,
  precision = 2,
  disabled = false,
  currency = '$',
}: BudgetFieldProps) {
  return (
    <FieldGroup
      label={label}
      description={description}
      required={required}
    >
      <NumberInput
        placeholder="0.00"
        min={min}
        max={max}
        decimalScale={precision}
        prefix={currency}
        disabled={disabled}
        {...form.getInputProps(fieldName)}
      />
    </FieldGroup>
  );
}

// Enable/disable switch field
export interface EnableFieldProps extends BaseFieldProps {
  enabledLabel?: string;
  disabledLabel?: string;
}

export function EnableField({
  form,
  fieldName,
  label = 'Status',
  description,
  enabledLabel = 'Enabled',
  disabledLabel = 'Disabled',
  disabled = false,
}: EnableFieldProps) {
  const isEnabled = form.values[fieldName] as boolean;
  
  return (
    <FieldGroup
      label={label}
      description={description}
    >
      <Group>
        <Switch
          size="md"
          disabled={disabled}
          {...form.getInputProps(fieldName, { type: 'checkbox' })}
        />
        <Text size="sm" c={isEnabled ? 'green' : 'dimmed'}>
          {isEnabled ? enabledLabel : disabledLabel}
        </Text>
      </Group>
    </FieldGroup>
  );
}

// Priority field component with standard range
export interface PriorityFieldProps extends BaseFieldProps {
  min?: number;
  max?: number;
}

export function PriorityField({
  form,
  fieldName,
  label = 'Priority',
  description = 'Higher values have higher priority (0-1000)',
  required = false,
  min = 0,
  max = 1000,
  disabled = false,
}: PriorityFieldProps) {
  return (
    <FieldGroup
      label={label}
      description={description}
      required={required}
    >
      <NumberInput
        placeholder="0"
        min={min}
        max={max}
        disabled={disabled}
        {...form.getInputProps(fieldName)}
      />
    </FieldGroup>
  );
}
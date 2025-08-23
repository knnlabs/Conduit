'use client';

import {
  Textarea,
  MultiSelect,
  NumberInput,
  Text,
} from '@mantine/core';
import { FieldGroup } from './BasicFormFields';
import { BaseFieldProps } from './FormFieldTypes';

// Description field component
export interface DescriptionFieldProps extends BaseFieldProps {
  placeholder?: string;
  minRows?: number;
  maxRows?: number;
}

export function DescriptionField({
  form,
  fieldName,
  label = 'Description',
  placeholder = 'Enter description (optional)',
  description,
  required = false,
  minRows = 3,
  maxRows = 6,
  disabled = false,
}: DescriptionFieldProps) {
  return (
    <FieldGroup
      label={label}
      description={description}
      required={required}
    >
      <Textarea
        placeholder={placeholder}
        minRows={minRows}
        maxRows={maxRows}
        autosize
        disabled={disabled}
        {...form.getInputProps(fieldName)}
      />
    </FieldGroup>
  );
}

// Capabilities multi-select field
export interface CapabilitiesFieldProps extends BaseFieldProps {
  data: Array<{ value: string; label: string; }>;
}

export function CapabilitiesField({
  form,
  fieldName,
  label = 'Capabilities',
  description = 'Select the capabilities this item supports',
  required = false,
  data,
  disabled = false,
}: CapabilitiesFieldProps) {
  return (
    <FieldGroup
      label={label}
      description={description}
      required={required}
    >
      <MultiSelect
        placeholder="Select capabilities"
        data={data}
        searchable
        clearable
        disabled={disabled}
        {...form.getInputProps(fieldName)}
      />
    </FieldGroup>
  );
}

// Rate limit field component
export interface RateLimitFieldProps extends BaseFieldProps {
  min?: number;
  max?: number;
  unit?: string;
}

export function RateLimitField({
  form,
  fieldName,
  label = 'Rate Limit',
  description = 'Maximum requests per minute (0 = unlimited)',
  required = false,
  min = 0,
  max = 10000,
  unit = 'req/min',
  disabled = false,
}: RateLimitFieldProps) {
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
        rightSection={<Text size="xs" c="dimmed">{unit}</Text>}
        disabled={disabled}
        {...form.getInputProps(fieldName)}
      />
    </FieldGroup>
  );
}
'use client';

import {
  TextInput,
  NumberInput,
  Textarea,
  Switch,
  PasswordInput,
  MultiSelect,
  Stack,
  Group,
  Text,
  Alert,
  Card,
  Divider,
} from '@mantine/core';
import { IconAlertCircle, IconInfoCircle } from '@tabler/icons-react';
import { validators } from '@/lib/utils/form-validators';
import { UseFormReturnType } from '@mantine/form';

// Base field group component for consistent layout
export interface FieldGroupProps {
  label?: string;
  description?: string;
  required?: boolean;
  children: React.ReactNode;
  gap?: 'xs' | 'sm' | 'md' | 'lg';
  className?: string;
}

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
export interface NameFieldProps {
  form: UseFormReturnType<Record<string, unknown>>; // Generic form - needs to support any form structure
  fieldName: string;
  label?: string;
  placeholder?: string;
  description?: string;
  required?: boolean;
  minLength?: number;
  maxLength?: number;
  disabled?: boolean;
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
export interface BudgetFieldProps {
  form: UseFormReturnType<Record<string, unknown>>; // Generic form - needs to support any form structure
  fieldName: string;
  label?: string;
  description?: string;
  required?: boolean;
  min?: number;
  max?: number;
  precision?: number;
  disabled?: boolean;
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
export interface EnableFieldProps {
  form: UseFormReturnType<Record<string, unknown>>;
  fieldName: string;
  label?: string;
  description?: string;
  enabledLabel?: string;
  disabledLabel?: string;
  disabled?: boolean;
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
  const isEnabled = form.values[fieldName];
  
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
export interface PriorityFieldProps {
  form: UseFormReturnType<Record<string, unknown>>;
  fieldName: string;
  label?: string;
  description?: string;
  required?: boolean;
  min?: number;
  max?: number;
  disabled?: boolean;
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

// API Key field component
export interface ApiKeyFieldProps {
  form: UseFormReturnType<Record<string, unknown>>;
  fieldName: string;
  label?: string;
  placeholder?: string;
  description?: string;
  required?: boolean;
  disabled?: boolean;
}

export function ApiKeyField({
  form,
  fieldName,
  label = 'API Key',
  placeholder = 'Enter your API key',
  description = 'Your API key will be encrypted and stored securely',
  required = true,
  disabled = false,
}: ApiKeyFieldProps) {
  return (
    <FieldGroup
      label={label}
      description={description}
      required={required}
    >
      <PasswordInput
        placeholder={placeholder}
        disabled={disabled}
        autoComplete="off"
        aria-autocomplete="none"
        list="autocompleteOff"
        data-form-type="other"
        data-lpignore="true"
        {...form.getInputProps(fieldName)}
      />
    </FieldGroup>
  );
}

// Description field component
export interface DescriptionFieldProps {
  form: UseFormReturnType<Record<string, unknown>>;
  fieldName: string;
  label?: string;
  placeholder?: string;
  description?: string;
  required?: boolean;
  minRows?: number;
  maxRows?: number;
  disabled?: boolean;
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

// Endpoint URL field component
export interface EndpointFieldProps {
  form: UseFormReturnType<Record<string, unknown>>;
  fieldName: string;
  label?: string;
  placeholder?: string;
  description?: string;
  required?: boolean;
  disabled?: boolean;
}

export function EndpointField({
  form,
  fieldName,
  label = 'API Endpoint',
  placeholder = 'https://api.example.com',
  description = 'The base URL for API requests',
  required = true,
  disabled = false,
}: EndpointFieldProps) {
  return (
    <FieldGroup
      label={label}
      description={description}
      required={required}
    >
      <TextInput
        placeholder={placeholder}
        disabled={disabled}
        {...form.getInputProps(fieldName)}
      />
    </FieldGroup>
  );
}

// Capabilities multi-select field
export interface CapabilitiesFieldProps {
  form: UseFormReturnType<Record<string, unknown>>;
  fieldName: string;
  label?: string;
  description?: string;
  required?: boolean;
  data: Array<{ value: string; label: string; }>;
  disabled?: boolean;
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
export interface RateLimitFieldProps {
  form: UseFormReturnType<Record<string, unknown>>;
  fieldName: string;
  label?: string;
  description?: string;
  required?: boolean;
  min?: number;
  max?: number;
  unit?: string;
  disabled?: boolean;
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

// Form section divider with label
export interface FormSectionProps {
  title: string;
  description?: string;
  children: React.ReactNode;
  collapsible?: boolean;
  defaultCollapsed?: boolean;
}

export function FormSection({
  title,
  description,
  children,
}: FormSectionProps) {
  return (
    <Stack gap="md">
      <Divider
        label={
          <Group gap="xs">
            <Text fw={500}>{title}</Text>
          </Group>
        }
        labelPosition="left"
      />
      {description && (
        <Text size="sm" c="dimmed">
          {description}
        </Text>
      )}
      {children}
    </Stack>
  );
}

// Info alert component for form guidance
export interface FormInfoAlertProps {
  title?: string;
  message: string;
  type?: 'info' | 'warning' | 'error';
  variant?: 'light' | 'filled' | 'outline';
}

export function FormInfoAlert({
  title,
  message,
  type = 'info',
  variant = 'light',
}: FormInfoAlertProps) {
  const colors = {
    info: 'blue',
    warning: 'orange',
    error: 'red',
  };

  const icons = {
    info: <IconInfoCircle size={16} />,
    warning: <IconAlertCircle size={16} />,
    error: <IconAlertCircle size={16} />,
  };

  return (
    <Alert
      icon={icons[type]}
      title={title}
      color={colors[type]}
      variant={variant}
    >
      {message}
    </Alert>
  );
}

// Form card container for grouping related fields
export interface FormCardProps {
  title?: string;
  description?: string;
  children: React.ReactNode;
  withBorder?: boolean;
  padding?: 'xs' | 'sm' | 'md' | 'lg';
}

export function FormCard({
  title,
  description,
  children,
  withBorder = true,
  padding = 'md',
}: FormCardProps) {
  return (
    <Card withBorder={withBorder} padding={padding}>
      {(title ?? description) && (
        <Card.Section inheritPadding py="sm" withBorder>
          {title && <Text fw={500}>{title}</Text>}
          {description && <Text size="sm" c="dimmed">{description}</Text>}
        </Card.Section>
      )}
      <Card.Section inheritPadding py="sm">
        {children}
      </Card.Section>
    </Card>
  );
}

// Validation helpers for common form field patterns
export const formValidation = {
  // Standard name validation
  name: (minLength = 3, maxLength = 100) => (value: string) => {
    const requiredError = validators.required('Name')(value);
    if (requiredError) return requiredError;
    const minLengthError = validators.minLength('Name', minLength)(value);
    if (minLengthError) return minLengthError;
    const maxLengthError = validators.maxLength('Name', maxLength)(value);
    if (maxLengthError) return maxLengthError;
    return null;
  },

  // API key validation
  apiKey: validators.required('API Key'),

  // Budget validation
  budget: validators.positiveNumber('Budget'),

  // Priority validation
  priority: (min = 0, max = 1000) => (value: number | undefined) => {
    if (value === undefined) return null;
    if (value < min) return `Priority must be at least ${min}`;
    if (value > max) return `Priority must be no more than ${max}`;
    return null;
  },

  // Rate limit validation
  rateLimit: (value: number | undefined) => {
    if (value === undefined) return null;
    if (value < 0) return 'Rate limit must be non-negative';
    if (!Number.isInteger(value)) return 'Rate limit must be a whole number';
    return null;
  },

  // URL validation
  url: validators.url('URL'),

  // Array minimum length validation
  capabilities: (minLength = 1) => validators.arrayMinLength('capability', minLength),
};
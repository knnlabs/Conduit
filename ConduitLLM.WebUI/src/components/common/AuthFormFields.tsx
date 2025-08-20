'use client';

import {
  TextInput,
  PasswordInput,
} from '@mantine/core';
import { FieldGroup } from './BasicFormFields';
import { BaseFieldProps } from './FormFieldTypes';

// API Key field component
export interface ApiKeyFieldProps extends BaseFieldProps {
  placeholder?: string;
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

// Endpoint URL field component
export interface EndpointFieldProps extends BaseFieldProps {
  placeholder?: string;
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
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

// Common form field interface
export interface BaseFieldProps {
  form: UseFormReturnType<Record<string, unknown>>; // Generic form - needs to support any form structure
  fieldName: string;
  label?: string;
  description?: string;
  required?: boolean;
  disabled?: boolean;
}
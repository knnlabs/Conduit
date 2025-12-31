/**
 * Dynamic parameter types for model-specific UI controls
 */

export type ParameterType = 
  | 'slider' 
  | 'select' 
  | 'text'
  | 'input'  // Added to support Replicate schema conversion
  | 'number' 
  | 'toggle' 
  | 'checkbox'  // Similar to toggle but for checkbox-style boolean inputs
  | 'color' 
  | 'resolution'
  | 'textarea'
  | 'media-upload'
  | 'media_upload'
  | 'file-upload'
  | 'file_upload';

export interface BaseParameter {
  type: ParameterType;
  name?: string;  // Parameter field name (e.g., "image_url")
  label: string;
  description?: string;
  default?: unknown;
  required?: boolean;
  visible?: boolean;
  metadata?: Record<string, unknown>; // Additional metadata for special handling
  dependsOn?: {
    parameter: string;
    value: unknown;
  };
}

export interface SliderParameter extends BaseParameter {
  type: 'slider';
  min: number;
  max: number;
  step: number;
  default?: number;
  unit?: string;
  marks?: Array<{ value: number; label: string }>;
}

export interface SelectParameter extends BaseParameter {
  type: 'select';
  options: Array<{
    value: string;
    label: string;
    description?: string;
  }>;
  default?: string;
  multiple?: boolean;
}

export interface TextParameter extends BaseParameter {
  type: 'text';
  placeholder?: string;
  maxLength?: number;
  pattern?: string;
  default?: string;
}

export interface InputParameter extends BaseParameter {
  type: 'input';
  placeholder?: string;
  maxLength?: number;
  pattern?: string;
  default?: string;
}

export interface TextareaParameter extends BaseParameter {
  type: 'textarea';
  placeholder?: string;
  rows?: number;
  maxLength?: number;
  default?: string;
}

export interface NumberParameter extends BaseParameter {
  type: 'number';
  min?: number;
  max?: number;
  step?: number;
  default?: number;
  precision?: number;
}

export interface ToggleParameter extends BaseParameter {
  type: 'toggle';
  default?: boolean;
  onLabel?: string;
  offLabel?: string;
}

export interface CheckboxParameter extends BaseParameter {
  type: 'checkbox';
  default?: boolean;
  checkboxLabel?: string;  // Label displayed next to the checkbox
}

export interface ColorParameter extends BaseParameter {
  type: 'color';
  default?: string;
  format?: 'hex' | 'rgb' | 'hsl';
  swatches?: string[];
}

export interface ResolutionParameter extends BaseParameter {
  type: 'resolution';
  options: Array<{
    value: string;
    label: string;
    width: number;
    height: number;
    aspectRatio?: string;
  }>;
  default?: string;
  allowCustom?: boolean;
}

export interface MediaUploadParameter extends BaseParameter {
  type: 'media-upload' | 'media_upload' | 'file-upload' | 'file_upload';
  accept?: string;  // MIME types or extensions
  maxSize?: number; // Max file size in bytes
  default?: string; // Default URL
}

export type DynamicParameter = 
  | SliderParameter
  | SelectParameter
  | TextParameter
  | InputParameter
  | TextareaParameter
  | NumberParameter
  | ToggleParameter
  | CheckboxParameter
  | ColorParameter
  | ResolutionParameter
  | MediaUploadParameter;

export type ParameterValues = Record<string, unknown>;

export type ParameterContext = 'chat' | 'image' | 'video' | 'audio';

export interface ParameterSet {
  context: ParameterContext;
  parameters: Record<string, DynamicParameter>;
  values: ParameterValues;
  presets?: Array<{
    name: string;
    description?: string;
    values: ParameterValues;
  }>;
}

// Type guards
export const isSliderParameter = (param: DynamicParameter): param is SliderParameter => 
  param.type === 'slider';

export const isSelectParameter = (param: DynamicParameter): param is SelectParameter => 
  param.type === 'select';

export const isTextParameter = (param: DynamicParameter): param is TextParameter => 
  param.type === 'text';

export const isInputParameter = (param: DynamicParameter): param is InputParameter => 
  param.type === 'input';

export const isTextareaParameter = (param: DynamicParameter): param is TextareaParameter => 
  param.type === 'textarea';

export const isNumberParameter = (param: DynamicParameter): param is NumberParameter => 
  param.type === 'number';

export const isToggleParameter = (param: DynamicParameter): param is ToggleParameter => 
  param.type === 'toggle';

export const isColorParameter = (param: DynamicParameter): param is ColorParameter => 
  param.type === 'color';

export const isResolutionParameter = (param: DynamicParameter): param is ResolutionParameter => 
  param.type === 'resolution';
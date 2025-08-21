/**
 * Dynamic parameter types for model-specific UI controls
 */

export type ParameterType = 
  | 'slider' 
  | 'select' 
  | 'text' 
  | 'number' 
  | 'toggle' 
  | 'color' 
  | 'resolution'
  | 'textarea';

export interface BaseParameter {
  type: ParameterType;
  label: string;
  description?: string;
  default?: any;
  required?: boolean;
  visible?: boolean;
  dependsOn?: {
    parameter: string;
    value: any;
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

export type DynamicParameter = 
  | SliderParameter
  | SelectParameter
  | TextParameter
  | TextareaParameter
  | NumberParameter
  | ToggleParameter
  | ColorParameter
  | ResolutionParameter;

export type ParameterValues = Record<string, any>;

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
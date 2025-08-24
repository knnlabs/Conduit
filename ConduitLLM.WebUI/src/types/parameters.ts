// Parameter definition types for UI generation
export interface BaseParameterDefinition {
  type: 'slider' | 'select' | 'input' | 'number' | 'checkbox' | 'textarea';
  label?: string;
  description?: string;
  default?: unknown;
  required?: boolean;
  placeholder?: string;
}

export interface SliderParameterDefinition extends BaseParameterDefinition {
  type: 'slider';
  min: number;
  max: number;
  step?: number;
  default?: number;
  marks?: Array<{ value: number; label: string }>;
}

export interface SelectParameterDefinition extends BaseParameterDefinition {
  type: 'select';
  options: Array<{
    value: string | number;
    label: string;
  }>;
  default?: string | number;
  clearable?: boolean;
}

export interface InputParameterDefinition extends BaseParameterDefinition {
  type: 'input';
  default?: string;
  maxLength?: number;
}

export interface NumberParameterDefinition extends BaseParameterDefinition {
  type: 'number';
  min?: number;
  max?: number;
  step?: number;
  default?: number;
  precision?: number;
}

export interface CheckboxParameterDefinition extends BaseParameterDefinition {
  type: 'checkbox';
  default?: boolean;
}

export interface TextareaParameterDefinition extends BaseParameterDefinition {
  type: 'textarea';
  default?: string;
  rows?: number;
  maxLength?: number;
}

export type ParameterDefinition = 
  | SliderParameterDefinition
  | SelectParameterDefinition
  | InputParameterDefinition
  | NumberParameterDefinition
  | CheckboxParameterDefinition
  | TextareaParameterDefinition;

export interface ParametersSchema {
  [key: string]: ParameterDefinition;
}

// Type guards
export function isSliderParameter(param: ParameterDefinition): param is SliderParameterDefinition {
  return param.type === 'slider';
}

export function isSelectParameter(param: ParameterDefinition): param is SelectParameterDefinition {
  return param.type === 'select';
}

export function isInputParameter(param: ParameterDefinition): param is InputParameterDefinition {
  return param.type === 'input';
}

export function isNumberParameter(param: ParameterDefinition): param is NumberParameterDefinition {
  return param.type === 'number';
}

export function isCheckboxParameter(param: ParameterDefinition): param is CheckboxParameterDefinition {
  return param.type === 'checkbox';
}

export function isTextareaParameter(param: ParameterDefinition): param is TextareaParameterDefinition {
  return param.type === 'textarea';
}

// Parse and validate parameter schema
export function parseParametersSchema(jsonString: string): ParametersSchema | null {
  try {
    const parsed = JSON.parse(jsonString) as unknown;
    if (typeof parsed !== 'object' || parsed === null) {
      return null;
    }
    return parsed as ParametersSchema;
  } catch {
    return null;
  }
}
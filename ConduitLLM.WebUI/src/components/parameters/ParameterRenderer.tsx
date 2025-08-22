'use client';

import { memo } from 'react';
import { SliderControl } from './controls/SliderControl';
import { SelectControl } from './controls/SelectControl';
import { TextControl } from './controls/TextControl';
import { TextareaControl } from './controls/TextareaControl';
import { NumberControl } from './controls/NumberControl';
import { ToggleControl } from './controls/ToggleControl';
import { ColorControl } from './controls/ColorControl';
import { ResolutionControl } from './controls/ResolutionControl';
import type { DynamicParameter, ParameterContext } from './types/parameters';

interface ParameterRendererProps {
  parameter: DynamicParameter;
  value: unknown;
  onChange: (value: unknown) => void;
  context: ParameterContext;
  disabled?: boolean;
}

const ParameterRendererComponent = ({
  parameter,
  value,
  onChange,
  context,
  disabled = false,
}: ParameterRendererProps) => {
  switch (parameter.type) {
    case 'slider':
      return (
        <SliderControl
          parameter={parameter}
          value={value as number}
          onChange={onChange}
          disabled={disabled}
        />
      );
    
    case 'select':
      return (
        <SelectControl
          parameter={parameter}
          value={value as string | string[]}
          onChange={onChange}
          disabled={disabled}
        />
      );
    
    case 'text':
      return (
        <TextControl
          parameter={parameter}
          value={value as string}
          onChange={onChange}
          disabled={disabled}
        />
      );
    
    case 'textarea':
      return (
        <TextareaControl
          parameter={parameter}
          value={value as string}
          onChange={onChange}
          disabled={disabled}
        />
      );
    
    case 'number':
      return (
        <NumberControl
          parameter={parameter}
          value={value as number}
          onChange={onChange}
          disabled={disabled}
        />
      );
    
    case 'toggle':
      return (
        <ToggleControl
          parameter={parameter}
          value={value as boolean}
          onChange={onChange}
          disabled={disabled}
        />
      );
    
    case 'color':
      return (
        <ColorControl
          parameter={parameter}
          value={value as string}
          onChange={onChange}
          disabled={disabled}
        />
      );
    
    case 'resolution':
      return (
        <ResolutionControl
          parameter={parameter}
          value={value as string}
          onChange={onChange}
          disabled={disabled}
          context={context}
        />
      );
    
    default:
      console.warn(`Unknown parameter type: ${(parameter as { type: string }).type}`);
      return null;
  }
};

ParameterRendererComponent.displayName = 'ParameterRenderer';

export const ParameterRenderer = memo(ParameterRendererComponent);
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
import { MediaUploadControl } from './controls/MediaUploadControl';
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
  // Check if this is a media URL field based on the parameter name
  const isMediaUrlField = (param: DynamicParameter): boolean => {
    const name = param.name ? param.name.toLowerCase() : '';
    const label = param.label ? param.label.toLowerCase() : '';
    const description = param.description ? param.description.toLowerCase() : '';
    
    // Check for common media URL field patterns
    const mediaPatterns = [
      'image_url', 'imageurl', 'image url',
      'video_url', 'videourl', 'video url',
      'audio_url', 'audiourl', 'audio url',
      'media_url', 'mediaurl', 'media url',
      'file_url', 'fileurl', 'file url',
      'start_image', 'startimage', 'start image',
      'end_image', 'endimage', 'end image',
      'init_image', 'initimage', 'init image',
    ];
    
    return mediaPatterns.some(pattern => 
      (name.includes(pattern)) || 
      (label.includes(pattern)) ||
      (description.includes(pattern))
    );
  };

  // If it's a text/input field that looks like a media URL field, use MediaUploadControl
  if ((parameter.type === 'text' || parameter.type === 'input') && isMediaUrlField(parameter)) {
    return (
      <MediaUploadControl
        parameter={parameter}
        value={value as string}
        onChange={onChange}
        disabled={disabled}
      />
    );
  }

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
    case 'input':  // Handle 'input' type the same as 'text'
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
    
    case 'media-upload':
    case 'media_upload':
    case 'file-upload':
    case 'file_upload':
      return (
        <MediaUploadControl
          parameter={parameter}
          value={value as string}
          onChange={onChange}
          disabled={disabled}
        />
      );
    
    default:
      console.warn(`Unknown parameter type: ${(parameter as { type: string }).type}`);
      return null;
  }
};

ParameterRendererComponent.displayName = 'ParameterRenderer';

export const ParameterRenderer = memo(ParameterRendererComponent);
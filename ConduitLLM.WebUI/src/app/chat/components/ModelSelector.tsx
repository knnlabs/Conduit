import { Select } from '@mantine/core';
import { useMemo } from 'react';

interface Model {
  id: string;
  displayName: string;
  supportsVision?: boolean;
}

interface ModelSelectorProps {
  label?: string;
  placeholder?: string;
  value: string | null;
  onChange: (value: string | null) => void;
  modelData?: Model[];
  style?: React.CSSProperties;
  disabled?: boolean;
}

export function ModelSelector({
  label = "Model",
  placeholder = "Select a model",
  value,
  onChange,
  modelData,
  style,
  disabled = false
}: ModelSelectorProps) {
  // Convert model data to the format expected by the Select component
  const models = useMemo(() => 
    modelData?.map(m => ({
      value: m.id,
      label: m.displayName
    })) ?? []
  , [modelData]);

  return (
    <Select
      label={label}
      placeholder={placeholder}
      value={value}
      onChange={onChange}
      data={models || []}
      searchable
      nothingFoundMessage="No matching models found"
      style={style}
      disabled={disabled}
    />
  );
}
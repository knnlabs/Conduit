import { Select } from '@mantine/core';
import { useMemo } from 'react';
import type { DiscoveryModel } from '@/app/chat/hooks/useDiscoveryModels';

interface ModelSelectorProps {
  label?: string;
  placeholder?: string;
  value: string | null;
  onChange: (value: string | null) => void;
  modelData?: DiscoveryModel[];
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
      label: m.display_name ?? m.id
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
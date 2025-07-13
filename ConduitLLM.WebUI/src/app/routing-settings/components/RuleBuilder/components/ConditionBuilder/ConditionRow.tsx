'use client';

import { useState, useEffect } from 'react';
import {
  Card,
  Group,
  Select,
  TextInput,
  NumberInput,
  ActionIcon,
  Tooltip,
  Badge,
  Textarea,
} from '@mantine/core';
import { IconTrash, IconGripVertical } from '@tabler/icons-react';
import { RoutingCondition } from '../../../../types/routing';
import { CONDITION_FIELDS, getOperatorsForField, getValueInputType } from '../../utils/ruleBuilder';

interface ConditionRowProps {
  condition: Omit<RoutingCondition, 'logicalOperator'>;
  index: number;
  onUpdate: (updates: Partial<RoutingCondition>) => void;
  onRemove: () => void;
  canRemove: boolean;
}

export function ConditionRow({ condition, index, onUpdate, onRemove, canRemove }: ConditionRowProps) {
  const [fieldConfig, setFieldConfig] = useState(CONDITION_FIELDS.find(f => f.value === condition.type));
  const [availableOperators, setAvailableOperators] = useState(getOperatorsForField(condition.type));
  const [valueInputType, setValueInputType] = useState(getValueInputType(condition.type, condition.operator));

  useEffect(() => {
    const config = CONDITION_FIELDS.find(f => f.value === condition.type);
    setFieldConfig(config);
    setAvailableOperators(getOperatorsForField(condition.type));
    setValueInputType(getValueInputType(condition.type, condition.operator));
  }, [condition.type, condition.operator]);

  const handleFieldChange = (value: string | null) => {
    if (!value) return;
    
    const newOperators = getOperatorsForField(value);
    const defaultOperator = newOperators[0]?.value || 'equals';
    
    onUpdate({
      type: value as any,
      operator: defaultOperator as any,
      value: '',
      field: undefined, // Reset field for new type
    });
  };

  const handleOperatorChange = (value: string | null) => {
    if (!value) return;
    
    onUpdate({
      operator: value as any,
      value: condition.operator !== value ? '' : condition.value, // Reset value if operator changed
    });
  };

  const handleValueChange = (value: any) => {
    onUpdate({ value });
  };

  const renderValueInput = () => {
    switch (valueInputType) {
      case 'number':
        return (
          <NumberInput
            placeholder="Enter number"
            value={typeof condition.value === 'number' ? condition.value : undefined}
            onChange={handleValueChange}
            decimalScale={2}
            style={{ flex: 1 }}
          />
        );

      case 'multiselect':
        return (
          <TextInput
            placeholder="Enter values separated by commas"
            value={condition.value}
            onChange={(e) => handleValueChange(e.target.value)}
            style={{ flex: 1 }}
            description="Use commas to separate multiple values"
          />
        );

      case 'textarea':
        return (
          <Textarea
            placeholder="Enter pattern or expression"
            value={condition.value}
            onChange={(e) => handleValueChange(e.target.value)}
            autosize
            minRows={1}
            maxRows={3}
            style={{ flex: 1 }}
          />
        );

      case 'select':
        // For boolean-like fields
        const options = (fieldConfig as any)?.options || [
          { value: 'true', label: 'True' },
          { value: 'false', label: 'False' },
        ];
        
        return (
          <Select
            placeholder="Select value"
            data={options}
            value={condition.value}
            onChange={handleValueChange}
            style={{ flex: 1 }}
          />
        );

      default:
        return (
          <TextInput
            placeholder="Enter value"
            value={condition.value}
            onChange={(e) => handleValueChange(e.target.value)}
            style={{ flex: 1 }}
          />
        );
    }
  };

  return (
    <Card withBorder p="md" bg={index % 2 === 0 ? 'white' : 'gray.0'}>
      <Group align="flex-start" gap="md">
        {/* Drag Handle */}
        <ActionIcon variant="subtle" color="gray" style={{ cursor: 'grab' }}>
          <IconGripVertical size={16} />
        </ActionIcon>

        {/* Condition Index */}
        <Badge variant="light" size="sm" color="blue">
          {index + 1}
        </Badge>

        {/* Field Selector */}
        <div style={{ flex: 1, minWidth: 120 }}>
          <Select
            label="Field"
            data={CONDITION_FIELDS}
            value={condition.type}
            onChange={handleFieldChange}
            searchable
          />
        </div>

        {/* Custom Field Input (for header/metadata types) */}
        {(condition.type === 'header' || condition.type === 'metadata') && (
          <div style={{ flex: 1, minWidth: 120 }}>
            <TextInput
              label="Field Name"
              placeholder={condition.type === 'header' ? 'Header name' : 'Metadata key'}
              value={condition.field || ''}
              onChange={(e) => onUpdate({ field: e.target.value })}
            />
          </div>
        )}

        {/* Operator Selector */}
        <div style={{ flex: 1, minWidth: 120 }}>
          <Select
            label="Operator"
            data={availableOperators}
            value={condition.operator}
            onChange={handleOperatorChange}
          />
        </div>

        {/* Value Input */}
        <div style={{ flex: 2, minWidth: 150 }}>
          <div style={{ marginBottom: 4 }}>
            <span style={{ fontSize: 14, fontWeight: 500 }}>Value</span>
          </div>
          {renderValueInput()}
        </div>

        {/* Remove Button */}
        <div style={{ paddingTop: 25 }}>
          <Tooltip
            label={canRemove ? 'Remove condition' : 'At least one condition is required'}
            position="left"
          >
            <ActionIcon
              color="red"
              variant="subtle"
              onClick={onRemove}
              disabled={!canRemove}
            >
              <IconTrash size={16} />
            </ActionIcon>
          </Tooltip>
        </div>
      </Group>
    </Card>
  );
}
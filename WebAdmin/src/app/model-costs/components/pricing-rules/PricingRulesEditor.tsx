'use client';

import { useEffect, useRef } from 'react';
import {
  Stack,
  Card,
  Divider,
  Alert,
  Group,
  Button,
  Text,
} from '@mantine/core';
import { IconAlertCircle, IconCheck, IconPlus } from '@tabler/icons-react';
import type { PricingRulesConfig } from '@knn_labs/conduit-admin-client';
import { usePricingRules } from '../../hooks/usePricingRules';
import { RuleConfigHeader } from './RuleConfigHeader';
import { RuleRow } from './RuleRow';
import { RulePreview } from './RulePreview';
import { ConstraintsEditor } from './ConstraintsEditor';

interface PricingRulesEditorProps {
  /** Initial configuration JSON string */
  initialConfig?: string;
  /** Callback when configuration changes */
  onChange?: (configJson: string) => void;
  /** Whether the editor is in read-only mode */
  readOnly?: boolean;
  /** Available parameter options from model series */
  parameterOptions?: Array<{
    key: string;
    label: string;
    type: 'string' | 'number' | 'boolean' | 'enum';
    options?: string[];
  }>;
}

/**
 * Visual editor for pricing rules configuration
 */
export function PricingRulesEditor({
  initialConfig,
  onChange,
  readOnly = false,
  parameterOptions = [],
}: PricingRulesEditorProps) {
  // Parse initial config
  let parsedInitialConfig: PricingRulesConfig | undefined;
  if (initialConfig) {
    try {
      parsedInitialConfig = JSON.parse(initialConfig) as PricingRulesConfig;
    } catch {
      // Invalid JSON, will use default config
    }
  }

  const {
    config,
    validationErrors,
    warnings,
    setPricingType,
    setUnitField,
    setDefaultRate,
    setConstraints,
    addRule,
    updateRule,
    removeRule,
    addCondition,
    removeCondition,
    getJson,
  } = usePricingRules({ initialConfig: parsedInitialConfig });

  // Use ref to store onChange to avoid it being a dependency in useEffect
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  // Notify parent of changes - use ref to prevent infinite loops
  useEffect(() => {
    onChangeRef.current?.(getJson());
  }, [config, getJson]);

  const isValid = validationErrors.length === 0;

  // Default parameter options if none provided
  const defaultParameterOptions = [
    { key: 'resolution', label: 'Resolution', type: 'enum' as const, options: ['480p', '720p', '1080p', '4k'] },
    { key: 'quality', label: 'Quality', type: 'enum' as const, options: ['standard', 'hd'] },
    { key: 'with_audio', label: 'With Audio', type: 'boolean' as const },
    { key: 'duration', label: 'Duration', type: 'number' as const },
    { key: 'style', label: 'Style', type: 'string' as const },
  ];

  const availableParams = parameterOptions.length > 0 ? parameterOptions : defaultParameterOptions;

  return (
    <Stack gap="md">
      {/* Header Configuration */}
      <RuleConfigHeader
        pricingType={config.pricingType}
        unitField={config.unitField ?? ''}
        defaultRate={config.defaultRate}
        onPricingTypeChange={setPricingType}
        onUnitFieldChange={setUnitField}
        onDefaultRateChange={setDefaultRate}
        readOnly={readOnly}
      />

      {/* Validation Constraints */}
      <ConstraintsEditor
        constraints={config.constraints}
        pricingType={config.pricingType}
        onChange={setConstraints}
        readOnly={readOnly}
      />

      <Divider label="Pricing Rules" labelPosition="center" />

      {/* Rules List */}
      <Stack gap="sm">
        {config.rules.length === 0 ? (
          <Card withBorder p="lg">
            <Stack align="center" gap="sm">
              <Text size="sm" c="dimmed">
                No rules defined. Add rules to customize pricing based on parameters.
              </Text>
              {!readOnly && (
                <Button
                  variant="light"
                  leftSection={<IconPlus size={16} />}
                  onClick={addRule}
                >
                  Add First Rule
                </Button>
              )}
            </Stack>
          </Card>
        ) : (
          config.rules.map((rule, index) => (
            <RuleRow
              key={index}
              rule={rule}
              index={index}
              parameterOptions={availableParams}
              onUpdate={(updates) => updateRule(index, updates)}
              onRemove={() => removeRule(index)}
              onAddCondition={(key, value) => addCondition(index, key, value)}
              onRemoveCondition={(key) => removeCondition(index, key)}
              readOnly={readOnly}
              validationErrors={validationErrors.filter(e => e.ruleIndex === index)}
            />
          ))
        )}

        {!readOnly && config.rules.length > 0 && (
          <Button
            variant="subtle"
            leftSection={<IconPlus size={16} />}
            onClick={addRule}
          >
            Add Rule
          </Button>
        )}
      </Stack>

      {/* Validation Status */}
      {validationErrors.length > 0 && (
        <Alert icon={<IconAlertCircle size={16} />} color="red" title="Validation Errors">
          <Stack gap="xs">
            {validationErrors.map((error, i) => (
              <Text key={i} size="sm">
                {error.ruleIndex !== undefined ? `Rule ${error.ruleIndex + 1}: ` : ''}
                {error.message}
              </Text>
            ))}
          </Stack>
        </Alert>
      )}

      {warnings.length > 0 && (
        <Alert icon={<IconAlertCircle size={16} />} color="yellow" title="Warnings">
          <Stack gap="xs">
            {warnings.map((warning, i) => (
              <Text key={i} size="sm">{warning}</Text>
            ))}
          </Stack>
        </Alert>
      )}

      {isValid && config.rules.length > 0 && (
        <Group gap="xs">
          <IconCheck size={16} color="green" />
          <Text size="sm" c="green">Configuration is valid</Text>
        </Group>
      )}

      <Divider label="Preview & Test" labelPosition="center" />

      {/* Preview Panel */}
      <RulePreview
        config={config}
        parameterOptions={availableParams}
      />
    </Stack>
  );
}

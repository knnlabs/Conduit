'use client';

import { Select, NumberInput, Group, Stack, Text, Tooltip, ActionIcon } from '@mantine/core';
import { IconInfoCircle, IconCurrencyDollar } from '@tabler/icons-react';
import type { PricingType } from '@/lib/admin-api';

interface RuleConfigHeaderProps {
  pricingType: PricingType;
  unitField: string;
  defaultRate: number;
  onPricingTypeChange: (type: PricingType) => void;
  onUnitFieldChange: (field: string) => void;
  onDefaultRateChange: (rate: number) => void;
  readOnly?: boolean;
}

const PRICING_TYPE_OPTIONS = [
  { value: 'per_unit', label: 'Per Unit (images, videos)' },
  { value: 'per_second', label: 'Per Second (video duration)' },
  { value: 'per_step', label: 'Per Step (inference steps)' },
];

const UNIT_FIELD_OPTIONS = [
  { value: 'ImageCount', label: 'Image Count' },
  { value: 'VideoCount', label: 'Video Count' },
  { value: 'VideoDurationSeconds', label: 'Video Duration (seconds)' },
  { value: 'InferenceSteps', label: 'Inference Steps' },
];

/**
 * Header section for pricing rules configuration
 */
export function RuleConfigHeader({
  pricingType,
  unitField,
  defaultRate,
  onPricingTypeChange,
  onUnitFieldChange,
  onDefaultRateChange,
  readOnly = false,
}: RuleConfigHeaderProps) {
  return (
    <Stack gap="md">
      <Group grow align="flex-start">
        <Select
          label={
            <Group gap={4}>
              <Text size="sm" fw={500}>Pricing Type</Text>
              <Tooltip
                label="Determines how the rate is multiplied: by count, duration, or steps"
                withArrow
              >
                <ActionIcon variant="subtle" size="xs">
                  <IconInfoCircle size={14} />
                </ActionIcon>
              </Tooltip>
            </Group>
          }
          description="How costs are calculated"
          data={PRICING_TYPE_OPTIONS}
          value={pricingType}
          onChange={(value) => value && onPricingTypeChange(value as PricingType)}
          disabled={readOnly}
        />

        <Select
          label={
            <Group gap={4}>
              <Text size="sm" fw={500}>Unit Field</Text>
              <Tooltip
                label="The usage field that provides the quantity for cost calculation"
                withArrow
              >
                <ActionIcon variant="subtle" size="xs">
                  <IconInfoCircle size={14} />
                </ActionIcon>
              </Tooltip>
            </Group>
          }
          description="Quantity source from usage"
          data={UNIT_FIELD_OPTIONS}
          value={unitField}
          onChange={(value) => value && onUnitFieldChange(value)}
          disabled={readOnly}
        />

        <NumberInput
          label={
            <Group gap={4}>
              <Text size="sm" fw={500}>Default Rate</Text>
              <Tooltip
                label="Rate applied when no rules match. Used as fallback pricing."
                withArrow
              >
                <ActionIcon variant="subtle" size="xs">
                  <IconInfoCircle size={14} />
                </ActionIcon>
              </Tooltip>
            </Group>
          }
          description="Fallback rate (USD)"
          value={defaultRate}
          onChange={(value) => onDefaultRateChange(typeof value === 'number' ? value : 0)}
          min={0}
          step={0.001}
          decimalScale={6}
          leftSection={<IconCurrencyDollar size={16} />}
          disabled={readOnly}
        />
      </Group>
    </Stack>
  );
}

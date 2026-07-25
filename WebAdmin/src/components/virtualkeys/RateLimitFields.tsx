'use client';

import { Alert, NumberInput, SimpleGrid, Stack, Text } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';

export interface RateLimitFormValues {
  rateLimitRpm?: number;
  rateLimitRpd?: number;
  rateLimitTpm?: number;
  maxParallelRequests?: number;
}

export type RateLimitField = keyof RateLimitFormValues;

interface RateLimitFieldsProps {
  values: RateLimitFormValues;
  onChange: (field: RateLimitField, value: number | undefined) => void;
  /** Wording differs slightly between a key and the group it belongs to. */
  scope?: 'key' | 'group';
}

/**
 * The rate-limit section shared by the create, edit and group modals.
 *
 * Every field is optional and blank means unlimited, so the descriptions say so rather than
 * leaving operators to infer it from an empty box.
 */
export function RateLimitFields({ values, onChange, scope = 'key' }: RateLimitFieldsProps) {
  const subject = scope === 'group' ? 'every key in this group' : 'this key';

  // 600 requests a minute against 100 a day is almost certainly a transposed pair: the daily
  // ceiling would be spent in ten seconds. Warn rather than block — it is legal, just unlikely.
  const suspiciousDailyLimit =
    typeof values.rateLimitRpm === 'number' &&
    typeof values.rateLimitRpd === 'number' &&
    values.rateLimitRpd < values.rateLimitRpm;

  const handle = (field: RateLimitField) => (value: string | number) => {
    onChange(field, typeof value === 'number' && Number.isFinite(value) ? value : undefined);
  };

  return (
    <Stack gap="sm">
      <SimpleGrid cols={{ base: 1, sm: 2 }}>
        <NumberInput
          label="Requests per minute"
          description={`Leave blank for no per-minute limit on ${subject}`}
          placeholder="Unlimited"
          min={1}
          step={1}
          allowDecimal={false}
          value={values.rateLimitRpm ?? ''}
          onChange={handle('rateLimitRpm')}
        />

        <NumberInput
          label="Requests per day"
          description="Rolling 24 hours, not a calendar day"
          placeholder="Unlimited"
          min={1}
          step={1}
          allowDecimal={false}
          value={values.rateLimitRpd ?? ''}
          onChange={handle('rateLimitRpd')}
        />

        <NumberInput
          label="Tokens per minute"
          description="Prompt plus completion tokens — what actually tracks cost"
          placeholder="Unlimited"
          min={1}
          step={1000}
          allowDecimal={false}
          value={values.rateLimitTpm ?? ''}
          onChange={handle('rateLimitTpm')}
        />

        <NumberInput
          label="Max parallel requests"
          description="Requests in flight at once, including open streams"
          placeholder="Unlimited"
          min={1}
          step={1}
          allowDecimal={false}
          value={values.maxParallelRequests ?? ''}
          onChange={handle('maxParallelRequests')}
        />
      </SimpleGrid>

      {suspiciousDailyLimit && (
        <Alert icon={<IconAlertTriangle size={16} />} color="yellow">
          <Text size="sm">
            The daily limit ({values.rateLimitRpd}) is below the per-minute limit ({values.rateLimitRpm}),
            so the daily allowance would be spent in under a minute. This is allowed — check the two
            values are not transposed.
          </Text>
        </Alert>
      )}
    </Stack>
  );
}

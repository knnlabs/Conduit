'use client';

import { useState, useMemo } from 'react';
import {
  Card,
  Stack,
  Group,
  Text,
  NumberInput,
  Select,
  Switch,
  TextInput,
  Button,
  Code,
  Tabs,
  Badge,
  Paper,
  Divider,
  Alert,
} from '@mantine/core';
import { IconPlayerPlay, IconCode, IconTestPipe, IconCurrencyDollar, IconCheck, IconX } from '@tabler/icons-react';
import type { PricingRulesConfig, PricingRule } from '@/lib/admin-api';

interface ParameterOption {
  key: string;
  label: string;
  type: 'string' | 'number' | 'boolean' | 'enum';
  options?: string[];
}

interface RulePreviewProps {
  config: PricingRulesConfig;
  parameterOptions: ParameterOption[];
}

interface SimulationResult {
  matchedRule?: PricingRule;
  usedDefaultRate: boolean;
  rate: number;
  quantity: number;
  cost: number;
}

/**
 * Preview panel for testing pricing rules
 */
export function RulePreview({ config, parameterOptions }: RulePreviewProps) {
  const [testParams, setTestParams] = useState<Record<string, string | number | boolean>>({});
  const [testQuantity, setTestQuantity] = useState<number>(1);

  // Run local simulation
  const simulationResult = useMemo((): SimulationResult | null => {
    if (Object.keys(testParams).length === 0 && config.rules.length > 0) {
      return null;
    }

    // Find matching rule (highest priority first)
    const sortedRules = [...config.rules].sort((a, b) => (b.priority ?? 0) - (a.priority ?? 0));

    const matchingRule = sortedRules.find(rule => {
      return Object.entries(rule.conditions).every(([key, expectedValue]) => {
        const actualValue = testParams[key];
        if (actualValue === undefined) return false;

        // Case-insensitive string comparison
        const expectedStr = String(expectedValue).toLowerCase();
        const actualStr = String(actualValue).toLowerCase();
        return expectedStr === actualStr;
      });
    });

    const usedDefaultRate = !matchingRule;
    const rate = matchingRule?.rate ?? config.defaultRate;
    const cost = rate * testQuantity;

    return {
      matchedRule: matchingRule,
      usedDefaultRate,
      rate,
      quantity: testQuantity,
      cost,
    };
  }, [config, testParams, testQuantity]);

  const updateTestParam = (key: string, value: string | number | boolean) => {
    setTestParams(prev => ({ ...prev, [key]: value }));
  };

  const clearTestParams = () => {
    setTestParams({});
  };

  // Get unique condition keys from all rules
  const usedConditionKeys = useMemo(() => {
    const keys = new Set<string>();
    config.rules.forEach(rule => {
      Object.keys(rule.conditions).forEach(key => keys.add(key));
    });
    return Array.from(keys);
  }, [config.rules]);

  // Parameters to show in test form
  const testableParams = parameterOptions.filter(p => usedConditionKeys.includes(p.key));

  const renderParamInput = (param: ParameterOption) => {
    switch (param.type) {
      case 'boolean':
        return (
          <Switch
            label={param.label}
            checked={testParams[param.key] === true}
            onChange={(e) => updateTestParam(param.key, e.currentTarget.checked)}
          />
        );
      case 'enum':
        return (
          <Select
            label={param.label}
            data={param.options?.map(o => ({ value: o, label: o })) ?? []}
            value={testParams[param.key] as string | undefined}
            onChange={(v) => v && updateTestParam(param.key, v)}
            clearable
            placeholder="Select..."
          />
        );
      case 'number':
        return (
          <NumberInput
            label={param.label}
            value={testParams[param.key] as number | undefined}
            onChange={(v) => typeof v === 'number' && updateTestParam(param.key, v)}
            placeholder="Enter value"
          />
        );
      default:
        return (
          <TextInput
            label={param.label}
            value={testParams[param.key] as string | undefined}
            onChange={(e) => updateTestParam(param.key, e.target.value)}
            placeholder="Enter value"
          />
        );
    }
  };

  return (
    <Card withBorder>
      <Tabs defaultValue="test">
        <Tabs.List>
          <Tabs.Tab value="test" leftSection={<IconTestPipe size={16} />}>
            Test Pricing
          </Tabs.Tab>
          <Tabs.Tab value="json" leftSection={<IconCode size={16} />}>
            JSON Preview
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="test" pt="md">
          <Stack gap="md">
            {testableParams.length === 0 && config.rules.length === 0 ? (
              <Alert color="blue" variant="light">
                Add rules with conditions to test pricing calculations
              </Alert>
            ) : (
              <>
                {/* Test Parameters */}
                <Stack gap="sm">
                  <Group justify="space-between">
                    <Text size="sm" fw={500}>Test Parameters</Text>
                    {Object.keys(testParams).length > 0 && (
                      <Button
                        variant="subtle"
                        size="xs"
                        onClick={clearTestParams}
                      >
                        Clear
                      </Button>
                    )}
                  </Group>

                  <Group grow>
                    {testableParams.map(param => (
                      <div key={param.key}>
                        {renderParamInput(param)}
                      </div>
                    ))}
                  </Group>

                  <NumberInput
                    label="Quantity"
                    description={`Number of ${config.unitField ?? 'units'}`}
                    value={testQuantity}
                    onChange={(v) => typeof v === 'number' && setTestQuantity(v)}
                    min={0}
                    step={1}
                    style={{ maxWidth: 200 }}
                  />
                </Stack>

                <Divider />

                {/* Simulation Results */}
                {simulationResult && (
                  <Paper withBorder p="md" radius="sm" bg="gray.0">
                    <Stack gap="sm">
                      <Group justify="space-between">
                        <Text size="sm" fw={500}>Simulation Result</Text>
                        <Badge
                          color={simulationResult.usedDefaultRate ? 'yellow' : 'green'}
                          variant="light"
                        >
                          {simulationResult.usedDefaultRate ? 'Default Rate' : 'Rule Matched'}
                        </Badge>
                      </Group>

                      {simulationResult.matchedRule && (
                        <Group gap="xs">
                          <IconCheck size={16} color="green" />
                          <Text size="sm">
                            Matched: <strong>{simulationResult.matchedRule.description ?? `Rule with priority ${simulationResult.matchedRule.priority}`}</strong>
                          </Text>
                        </Group>
                      )}

                      {simulationResult.usedDefaultRate && (
                        <Group gap="xs">
                          <IconX size={16} color="orange" />
                          <Text size="sm" c="dimmed">
                            No rules matched - using default rate
                          </Text>
                        </Group>
                      )}

                      <Group grow>
                        <Paper withBorder p="sm" radius="sm">
                          <Stack gap={4} align="center">
                            <Text size="xs" c="dimmed">Rate</Text>
                            <Text size="lg" fw={600}>
                              ${simulationResult.rate.toFixed(6)}
                            </Text>
                          </Stack>
                        </Paper>

                        <Paper withBorder p="sm" radius="sm">
                          <Stack gap={4} align="center">
                            <Text size="xs" c="dimmed">Quantity</Text>
                            <Text size="lg" fw={600}>
                              {simulationResult.quantity}
                            </Text>
                          </Stack>
                        </Paper>

                        <Paper withBorder p="sm" radius="sm" bg="blue.0">
                          <Stack gap={4} align="center">
                            <Text size="xs" c="dimmed">Total Cost</Text>
                            <Group gap={4}>
                              <IconCurrencyDollar size={20} />
                              <Text size="xl" fw={700} c="blue">
                                {simulationResult.cost.toFixed(6)}
                              </Text>
                            </Group>
                          </Stack>
                        </Paper>
                      </Group>
                    </Stack>
                  </Paper>
                )}

                {!simulationResult && (
                  <Alert color="gray" variant="light">
                    <Group gap="xs">
                      <IconPlayerPlay size={16} />
                      <Text size="sm">
                        Set test parameters to simulate pricing calculation
                      </Text>
                    </Group>
                  </Alert>
                )}
              </>
            )}
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="json" pt="md">
          <Code block style={{ maxHeight: 400, overflow: 'auto' }}>
            {JSON.stringify(config, null, 2)}
          </Code>
        </Tabs.Panel>
      </Tabs>
    </Card>
  );
}

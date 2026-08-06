import { useState, useCallback, useMemo } from 'react';
import type {
  PricingRulesConfig,
  PricingRule,
  PricingConstraints,
  PricingType,
  PricingValidationError,
} from '@/lib/admin-api';

interface UsePricingRulesOptions {
  initialConfig?: PricingRulesConfig;
}

interface UsePricingRulesReturn {
  config: PricingRulesConfig;
  validationErrors: PricingValidationError[];
  warnings: string[];
  isDirty: boolean;

  // Config-level operations
  setPricingType: (type: PricingType) => void;
  setUnitField: (field: string) => void;
  setDefaultRate: (rate: number) => void;
  setConstraints: (constraints: PricingConstraints | undefined) => void;

  // Rule operations
  addRule: () => void;
  updateRule: (index: number, rule: Partial<PricingRule>) => void;
  removeRule: (index: number) => void;
  moveRule: (fromIndex: number, toIndex: number) => void;

  // Condition operations
  addCondition: (ruleIndex: number, key: string, value: string | number | boolean) => void;
  updateCondition: (ruleIndex: number, key: string, value: string | number | boolean) => void;
  removeCondition: (ruleIndex: number, key: string) => void;

  // Utilities
  reset: () => void;
  setConfig: (config: PricingRulesConfig) => void;
  getJson: () => string;
  parseJson: (json: string) => boolean;
}

const DEFAULT_CONFIG: PricingRulesConfig = {
  version: '1.0',
  pricingType: 'per_unit',
  unitField: 'ImageCount',
  defaultRate: 0,
  rules: [],
};

const DEFAULT_UNIT_FIELDS: Record<PricingType, string> = {
  'per_unit': 'ImageCount',
  'per_second': 'VideoDurationSeconds',
  'per_step': 'InferenceSteps',
};

/**
 * Hook for managing pricing rules configuration state
 */
export function usePricingRules(options: UsePricingRulesOptions = {}): UsePricingRulesReturn {
  const [config, setConfigState] = useState<PricingRulesConfig>(
    options.initialConfig ?? { ...DEFAULT_CONFIG }
  );
  const [originalConfig] = useState<PricingRulesConfig>(
    options.initialConfig ?? { ...DEFAULT_CONFIG }
  );

  // Validate the current configuration
  const { validationErrors, warnings } = useMemo(() => {
    const errors: PricingValidationError[] = [];
    const warns: string[] = [];

    // Validate required fields
    if (!config.pricingType) {
      errors.push({ field: 'pricingType', message: 'Pricing type is required' });
    } else if (!['per_unit', 'per_second', 'per_step'].includes(config.pricingType)) {
      errors.push({
        field: 'pricingType',
        message: 'Invalid pricing type. Must be per_unit, per_second, or per_step'
      });
    }

    if (config.defaultRate < 0) {
      errors.push({ field: 'defaultRate', message: 'Default rate cannot be negative' });
    }

    // Validate rules
    config.rules.forEach((rule, index) => {
      if (rule.rate < 0) {
        errors.push({ field: 'rate', message: 'Rate cannot be negative', ruleIndex: index });
      }

      if (!rule.conditions || Object.keys(rule.conditions).length === 0) {
        warns.push(`Rule ${index + 1} has no conditions - will always match at its priority level`);
      }
    });

    // Check for duplicate conditions
    const conditionKeys = new Set<string>();
    config.rules.forEach((rule, index) => {
      const key = JSON.stringify(
        Object.entries(rule.conditions).sort(([a], [b]) => a.localeCompare(b))
      );
      if (conditionKeys.has(key)) {
        warns.push(`Rule ${index + 1} has duplicate conditions with another rule`);
      }
      conditionKeys.add(key);
    });

    // Validate constraints
    if (config.constraints) {
      const c = config.constraints;
      if (c.minDuration !== undefined && c.maxDuration !== undefined && c.minDuration > c.maxDuration) {
        errors.push({ field: 'constraints.minDuration', message: 'Min duration cannot exceed max duration' });
      }
      if (c.minSteps !== undefined && c.maxSteps !== undefined && c.minSteps > c.maxSteps) {
        errors.push({ field: 'constraints.minSteps', message: 'Min steps cannot exceed max steps' });
      }
    }

    return { validationErrors: errors, warnings: warns };
  }, [config]);

  // Check if config has changed
  const isDirty = useMemo(() => {
    return JSON.stringify(config) !== JSON.stringify(originalConfig);
  }, [config, originalConfig]);

  // Config-level operations
  const setPricingType = useCallback((type: PricingType) => {
    setConfigState(prev => ({
      ...prev,
      pricingType: type,
      unitField: DEFAULT_UNIT_FIELDS[type],
    }));
  }, []);

  const setUnitField = useCallback((field: string) => {
    setConfigState(prev => ({ ...prev, unitField: field }));
  }, []);

  const setDefaultRate = useCallback((rate: number) => {
    setConfigState(prev => ({ ...prev, defaultRate: rate }));
  }, []);

  const setConstraints = useCallback((constraints: PricingConstraints | undefined) => {
    setConfigState(prev => ({ ...prev, constraints }));
  }, []);

  // Rule operations
  const addRule = useCallback(() => {
    setConfigState(prev => ({
      ...prev,
      rules: [
        ...prev.rules,
        {
          conditions: {},
          rate: 0,
          priority: 0,
          description: '',
        },
      ],
    }));
  }, []);

  const updateRule = useCallback((index: number, updates: Partial<PricingRule>) => {
    setConfigState(prev => ({
      ...prev,
      rules: prev.rules.map((rule, i) =>
        i === index ? { ...rule, ...updates } : rule
      ),
    }));
  }, []);

  const removeRule = useCallback((index: number) => {
    setConfigState(prev => ({
      ...prev,
      rules: prev.rules.filter((rule, i) => i !== index),
    }));
  }, []);

  const moveRule = useCallback((fromIndex: number, toIndex: number) => {
    setConfigState(prev => {
      const newRules = [...prev.rules];
      const [removed] = newRules.splice(fromIndex, 1);
      newRules.splice(toIndex, 0, removed);
      return { ...prev, rules: newRules };
    });
  }, []);

  // Condition operations
  const addCondition = useCallback((ruleIndex: number, key: string, value: string | number | boolean) => {
    setConfigState(prev => ({
      ...prev,
      rules: prev.rules.map((rule, i) =>
        i === ruleIndex
          ? { ...rule, conditions: { ...rule.conditions, [key]: value } }
          : rule
      ),
    }));
  }, []);

  const updateCondition = useCallback((ruleIndex: number, key: string, value: string | number | boolean) => {
    addCondition(ruleIndex, key, value);
  }, [addCondition]);

  const removeCondition = useCallback((ruleIndex: number, key: string) => {
    setConfigState(prev => ({
      ...prev,
      rules: prev.rules.map((rule, i) => {
        if (i !== ruleIndex) return rule;
        const newConditions = { ...rule.conditions };
        delete newConditions[key];
        return { ...rule, conditions: newConditions };
      }),
    }));
  }, []);

  // Utilities
  const reset = useCallback(() => {
    setConfigState(options.initialConfig ?? { ...DEFAULT_CONFIG });
  }, [options.initialConfig]);

  const setConfig = useCallback((newConfig: PricingRulesConfig) => {
    setConfigState(newConfig);
  }, []);

  const getJson = useCallback(() => {
    return JSON.stringify(config, null, 2);
  }, [config]);

  const parseJson = useCallback((json: string): boolean => {
    try {
      const parsed = JSON.parse(json) as PricingRulesConfig;
      setConfigState(parsed);
      return true;
    } catch {
      return false;
    }
  }, []);

  return {
    config,
    validationErrors,
    warnings,
    isDirty,
    setPricingType,
    setUnitField,
    setDefaultRate,
    setConstraints,
    addRule,
    updateRule,
    removeRule,
    moveRule,
    addCondition,
    updateCondition,
    removeCondition,
    reset,
    setConfig,
    getJson,
    parseJson,
  };
}

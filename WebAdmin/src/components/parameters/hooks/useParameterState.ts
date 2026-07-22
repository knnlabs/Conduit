import { useState, useCallback, useMemo, useEffect, useRef } from 'react';
import type { DynamicParameter, ParameterValues } from '../types/parameters';

interface UseParameterStateOptions {
  parameters: Record<string, DynamicParameter> | string;
  initialValues?: ParameterValues;
  onChange?: (values: ParameterValues) => void;
  persistKey?: string;
}

const parameterValuesEqual = (left: unknown, right: unknown): boolean =>
  JSON.stringify(left) === JSON.stringify(right);

function isValidParameterValue(parameter: DynamicParameter, value: unknown): boolean {
  switch (parameter.type) {
    case 'select': {
      const allowedValues = new Set(parameter.options.map(option => option.value));
      return parameter.multiple
        ? Array.isArray(value) && value.every(item => typeof item === 'string' && allowedValues.has(item))
        : typeof value === 'string' && allowedValues.has(value);
    }
    case 'resolution':
      return typeof value === 'string' && value.length > 0 && (
        parameter.allowCustom === true || parameter.options.some(option => option.value === value)
      );
    case 'slider':
      return typeof value === 'number' && Number.isFinite(value) &&
        value >= parameter.min && value <= parameter.max;
    case 'number':
      return typeof value === 'number' && Number.isFinite(value) &&
        (parameter.min === undefined || value >= parameter.min) &&
        (parameter.max === undefined || value <= parameter.max);
    case 'toggle':
    case 'checkbox':
      return typeof value === 'boolean';
    case 'text':
    case 'input':
    case 'textarea':
    case 'color':
    case 'media-upload':
    case 'media_upload':
    case 'file-upload':
    case 'file_upload':
      return typeof value === 'string';
    default:
      return false;
  }
}

export function normalizeParameterValues(
  parameters: Record<string, DynamicParameter>,
  candidateValues: ParameterValues,
): ParameterValues {
  const normalized: ParameterValues = {};

  Object.entries(parameters).forEach(([key, parameter]) => {
    const candidate = candidateValues[key];
    if (isValidParameterValue(parameter, candidate)) {
      normalized[key] = candidate;
    } else if (isValidParameterValue(parameter, parameter.default)) {
      normalized[key] = parameter.default;
    }
  });

  return normalized;
}

export function useParameterState({
  parameters,
  initialValues = {},
  onChange,
  persistKey,
}: UseParameterStateOptions) {
  const parsedParameters = useMemo(() => {
    if (typeof parameters === 'string') {
      try {
        return JSON.parse(parameters) as Record<string, DynamicParameter>;
      } catch {
        return {};
      }
    }
    return parameters;
  }, [parameters]);

  const defaultValues = useMemo(() => {
    const defaults: ParameterValues = {};
    Object.entries(parsedParameters).forEach(([key, parameter]) => {
      if (parameter.default !== undefined) defaults[key] = parameter.default;
    });
    return defaults;
  }, [parsedParameters]);

  const loadPersistedValues = useCallback((): ParameterValues => {
    if (!persistKey) return {};

    try {
      const stored = localStorage.getItem(`parameters_${persistKey}`);
      return stored ? JSON.parse(stored) as ParameterValues : {};
    } catch {
      return {};
    }
  }, [persistKey]);

  const [values, setValues] = useState<ParameterValues>(() =>
    normalizeParameterValues(parsedParameters, {
      ...defaultValues,
      ...initialValues,
      ...loadPersistedValues(),
    }),
  );
  const previousPersistKey = useRef(persistKey);
  const previousDefaults = useRef(defaultValues);

  const updateValues = useCallback((newValues: ParameterValues) => {
    setValues(newValues);
    onChange?.(newValues);

    if (persistKey) {
      try {
        localStorage.setItem(`parameters_${persistKey}`, JSON.stringify(newValues));
      } catch (error) {
        console.error('Failed to persist parameter values:', error);
      }
    }
  }, [onChange, persistKey]);

  const updateValue = useCallback((key: string, value: unknown) => {
    updateValues({
      ...values,
      [key]: value,
    });
  }, [values, updateValues]);

  const resetValues = useCallback(() => {
    updateValues(defaultValues);
  }, [defaultValues, updateValues]);

  const clearPersisted = useCallback(() => {
    if (persistKey) {
      try {
        localStorage.removeItem(`parameters_${persistKey}`);
      } catch (error) {
        console.error('Failed to clear persisted values:', error);
      }
    }
  }, [persistKey]);

  const getSubmitValues = useCallback(() => {
    const submitValues: ParameterValues = {};
    Object.entries(values).forEach(([key, value]) => {
      const parameter = parsedParameters[key];
      if (parameter && !parameterValuesEqual(value, parameter.default)) submitValues[key] = value;
    });
    return submitValues;
  }, [values, parsedParameters]);

  const hasChanges = useMemo(() => {
    return Object.entries(values).some(([key, value]) => {
      const parameter = parsedParameters[key];
      return parameter && !parameterValuesEqual(value, parameter.default);
    });
  }, [values, parsedParameters]);

  // Refresh and normalize values whenever the selected model or its server schema changes.
  useEffect(() => {
    const modelChanged = previousPersistKey.current !== persistKey;
    const candidates = modelChanged ? loadPersistedValues() : { ...values };

    if (!modelChanged) {
      Object.entries(previousDefaults.current).forEach(([key, previousDefault]) => {
        if (parameterValuesEqual(candidates[key], previousDefault)) {
          delete candidates[key];
        }
      });
    }

    const normalizedValues = normalizeParameterValues(parsedParameters, candidates);
    if (!parameterValuesEqual(normalizedValues, values)) {
      setValues(normalizedValues);
      onChange?.(normalizedValues);

      if (persistKey) {
        try {
          localStorage.setItem(`parameters_${persistKey}`, JSON.stringify(normalizedValues));
        } catch (error) {
          console.error('Failed to persist normalized parameter values:', error);
        }
      }
    }

    previousPersistKey.current = persistKey;
    previousDefaults.current = defaultValues;
  }, [parsedParameters, persistKey]); // eslint-disable-line react-hooks/exhaustive-deps

  return {
    values,
    updateValues,
    updateValue,
    resetValues,
    clearPersisted,
    getSubmitValues,
    hasChanges,
    defaultValues,
    parameters: parsedParameters,
  };
}

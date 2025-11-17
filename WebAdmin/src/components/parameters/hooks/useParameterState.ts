import { useState, useCallback, useMemo, useEffect } from 'react';
import type { DynamicParameter, ParameterValues } from '../types/parameters';

interface UseParameterStateOptions {
  parameters: Record<string, DynamicParameter> | string;
  initialValues?: ParameterValues;
  onChange?: (values: ParameterValues) => void;
  persistKey?: string;
}

export function useParameterState({
  parameters,
  initialValues = {},
  onChange,
  persistKey,
}: UseParameterStateOptions) {
  // Parse parameters if string
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

  // Get default values from parameters
  const defaultValues = useMemo(() => {
    const defaults: ParameterValues = {};
    Object.entries(parsedParameters).forEach(([key, param]) => {
      if (param.default !== undefined) {
        defaults[key] = param.default;
      }
    });
    return defaults;
  }, [parsedParameters]);

  // Load persisted values if available
  const loadPersistedValues = useCallback((): ParameterValues => {
    if (!persistKey) return {};
    
    try {
      const stored = localStorage.getItem(`parameters_${persistKey}`);
      return stored ? JSON.parse(stored) as ParameterValues : {};
    } catch {
      return {};
    }
  }, [persistKey]);

  // Initialize state with priority: persisted > initial > defaults
  const [values, setValues] = useState<ParameterValues>(() => ({
    ...defaultValues,
    ...initialValues,
    ...loadPersistedValues(),
  }));

  // Update values
  const updateValues = useCallback((newValues: ParameterValues) => {
    setValues(newValues);
    onChange?.(newValues);
    
    // Persist if key provided
    if (persistKey) {
      try {
        localStorage.setItem(`parameters_${persistKey}`, JSON.stringify(newValues));
      } catch (error) {
        console.error('Failed to persist parameter values:', error);
      }
    }
  }, [onChange, persistKey]);

  // Update single value
  const updateValue = useCallback((key: string, value: unknown) => {
    updateValues({
      ...values,
      [key]: value,
    });
  }, [values, updateValues]);

  // Reset to defaults
  const resetValues = useCallback(() => {
    updateValues(defaultValues);
  }, [defaultValues, updateValues]);

  // Clear persisted values
  const clearPersisted = useCallback(() => {
    if (persistKey) {
      try {
        localStorage.removeItem(`parameters_${persistKey}`);
      } catch (error) {
        console.error('Failed to clear persisted values:', error);
      }
    }
  }, [persistKey]);

  // Get values for API submission (exclude defaults)
  const getSubmitValues = useCallback(() => {
    const submitValues: ParameterValues = {};
    Object.entries(values).forEach(([key, value]) => {
      const param = parsedParameters[key];
      // Only include non-default values
      if (param && value !== param.default) {
        submitValues[key] = value;
      }
    });
    return submitValues;
  }, [values, parsedParameters]);

  // Check if any values are non-default
  const hasChanges = useMemo(() => {
    return Object.entries(values).some(([key, value]) => {
      const param = parsedParameters[key];
      return param && value !== param.default;
    });
  }, [values, parsedParameters]);

  // Update when parameters change
  useEffect(() => {
    // If parameters changed, merge new defaults with existing values
    const newDefaults = defaultValues;
    const mergedValues = {
      ...newDefaults,
      ...values,
    };
    
    // Remove values for parameters that no longer exist
    const cleanedValues: ParameterValues = {};
    Object.keys(mergedValues).forEach(key => {
      if (parsedParameters[key]) {
        cleanedValues[key] = mergedValues[key];
      }
    });
    
    if (JSON.stringify(cleanedValues) !== JSON.stringify(values)) {
      setValues(cleanedValues);
    }
  }, [parsedParameters]); // eslint-disable-line react-hooks/exhaustive-deps

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
'use client';

import { useState, useEffect, useCallback } from 'react';
import { CreateRoutingRuleRequest } from '../../../types/routing';

export function useRuleValidation(rule: CreateRoutingRuleRequest) {
  const [errors, setErrors] = useState<string[]>([]);
  const [isValid, setIsValid] = useState(false);

  const validate = useCallback(() => {
    const newErrors: string[] = [];

    // Rule name validation
    if (!rule.name || rule.name.trim() === '') {
      newErrors.push('Rule name is required');
    } else if (rule.name.length < 3) {
      newErrors.push('Rule name must be at least 3 characters long');
    } else if (rule.name.length > 100) {
      newErrors.push('Rule name must be less than 100 characters');
    }

    // Priority validation
    if (rule.priority === undefined || rule.priority === null) {
      newErrors.push('Priority is required');
    } else if (rule.priority < 1 || rule.priority > 1000) {
      newErrors.push('Priority must be between 1 and 1000');
    }

    // Conditions validation
    if (!rule.conditions || rule.conditions.length === 0) {
      newErrors.push('At least one condition is required');
    } else {
      rule.conditions.forEach((condition, index) => {
        if (!condition.type) {
          newErrors.push(`Condition ${index + 1}: Type is required`);
        }

        if (!condition.operator) {
          newErrors.push(`Condition ${index + 1}: Operator is required`);
        }

        if (condition.value === undefined || condition.value === '') {
          if (condition.operator !== 'exists') {
            newErrors.push(`Condition ${index + 1}: Value is required`);
          }
        }

        // Field-specific validation
        if ((condition.type === 'header' || condition.type === 'metadata') && !condition.field) {
          newErrors.push(`Condition ${index + 1}: Field name is required for ${condition.type} type`);
        }

        // Value format validation
        if (condition.operator === 'between' && condition.value) {
          const parts = condition.value.toString().split(',');
          if (parts.length !== 2) {
            newErrors.push(`Condition ${index + 1}: "Between" operator requires two comma-separated values`);
          } else {
            const [min, max] = parts.map((p: string) => parseFloat(p.trim()));
            if (isNaN(min) || isNaN(max)) {
              newErrors.push(`Condition ${index + 1}: "Between" values must be numbers`);
            } else if (min >= max) {
              newErrors.push(`Condition ${index + 1}: First value must be less than second value`);
            }
          }
        }

        // Numeric validation for appropriate fields
        if ((condition.type === 'cost' || condition.type === 'load') && 
            condition.operator !== 'exists' && 
            condition.value !== '') {
          const numValue = parseFloat(condition.value.toString());
          if (isNaN(numValue)) {
            newErrors.push(`Condition ${index + 1}: ${condition.type} must be a number`);
          } else if (condition.type === 'cost' && numValue < 0) {
            newErrors.push(`Condition ${index + 1}: Cost cannot be negative`);
          }
        }

        // Regex validation
        if (condition.operator === 'regex' && condition.value) {
          try {
            new RegExp(condition.value.toString());
          } catch {
            newErrors.push(`Condition ${index + 1}: Invalid regular expression`);
          }
        }
      });
    }

    // Actions validation
    if (!rule.actions || rule.actions.length === 0) {
      newErrors.push('At least one action is required');
    } else {
      rule.actions.forEach((action, index) => {
        if (!action.type) {
          newErrors.push(`Action ${index + 1}: Type is required`);
        }

        // Target validation for route actions
        if (action.type === 'route' && (!action.target || action.target.trim() === '')) {
          newErrors.push(`Action ${index + 1}: Target provider is required for route actions`);
        }

        // Parameters validation
        if (action.parameters) {
          // Validate timeout parameter
          if (action.parameters.timeout !== undefined) {
            const timeoutValue = action.parameters.timeout;
            const timeout = parseFloat(typeof timeoutValue === 'string' ? timeoutValue : String(timeoutValue));
            if (isNaN(timeout) || timeout <= 0) {
              newErrors.push(`Action ${index + 1}: Timeout must be a positive number`);
            } else if (timeout > 300000) { // 5 minutes max
              newErrors.push(`Action ${index + 1}: Timeout cannot exceed 5 minutes (300000ms)`);
            }
          }

          // Validate weight parameter
          if (action.parameters.weight !== undefined) {
            const weightValue = action.parameters.weight;
            const weight = parseFloat(typeof weightValue === 'string' ? weightValue : String(weightValue));
            if (isNaN(weight) || weight < 0 || weight > 100) {
              newErrors.push(`Action ${index + 1}: Weight must be between 0 and 100`);
            }
          }

          // Validate max_retries parameter
          if (action.parameters.maxRetries !== undefined) {
            const retriesValue = action.parameters.maxRetries;
            const retries = parseInt(typeof retriesValue === 'string' ? retriesValue : String(retriesValue));
            if (isNaN(retries) || retries < 0 || retries > 10) {
              newErrors.push(`Action ${index + 1}: Max retries must be between 0 and 10`);
            }
          }
        }
      });
    }

    // Check for duplicate rule names (this would typically be done against existing rules)
    // For now, we'll just validate the format
    if (rule.name && !/^[a-zA-Z0-9\s\-_]+$/.test(rule.name)) {
      newErrors.push('Rule name can only contain letters, numbers, spaces, hyphens, and underscores');
    }

    setErrors(newErrors);
    setIsValid(newErrors.length === 0);
  }, [rule]);

  // Auto-validate when rule changes
  useEffect(() => {
    validate();
  }, [validate]);

  const validateField = useCallback((field: keyof CreateRoutingRuleRequest, value: unknown) => {
    const fieldErrors: string[] = [];

    switch (field) {
      case 'name':
        let nameValue = '';
        if (typeof value === 'string') {
          nameValue = value;
        } else if (value !== null && value !== undefined) {
          nameValue = String(value);
        }
        if (!nameValue || nameValue.trim() === '') {
          fieldErrors.push('Rule name is required');
        } else if (nameValue.length < 3) {
          fieldErrors.push('Rule name must be at least 3 characters long');
        } else if (nameValue.length > 100) {
          fieldErrors.push('Rule name must be less than 100 characters');
        } else if (!/^[a-zA-Z0-9\s\-_]+$/.test(nameValue)) {
          fieldErrors.push('Rule name can only contain letters, numbers, spaces, hyphens, and underscores');
        }
        break;

      case 'priority':
        if (value === undefined || value === null) {
          fieldErrors.push('Priority is required');
        } else {
          const priorityValue = typeof value === 'number' ? value : Number(value);
          if (isNaN(priorityValue) || priorityValue < 1 || priorityValue > 1000) {
            fieldErrors.push('Priority must be between 1 and 1000');
          }
        }
        break;

      case 'description':
        let descValue = '';
        if (typeof value === 'string') {
          descValue = value;
        } else if (value !== null && value !== undefined) {
          descValue = String(value);
        }
        if (descValue && descValue.length > 500) {
          fieldErrors.push('Description must be less than 500 characters');
        }
        break;
    }

    return fieldErrors;
  }, []);

  return {
    validate,
    validateField,
    errors,
    isValid,
    hasErrors: errors.length > 0,
  };
}
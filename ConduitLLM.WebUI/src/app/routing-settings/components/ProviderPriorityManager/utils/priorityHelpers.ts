interface ProviderPriority {
  providerId: string;
  providerName: string;
  priority: number;
  isEnabled: boolean;
  weight?: number;
}

/**
 * Validates that all provider priorities are unique
 */
export function validateUniquePriorities(providers: ProviderPriority[]): {
  isValid: boolean;
  errors: string[];
  duplicates: { priority: number; providers: string[] }[];
} {
  const errors: string[] = [];
  const duplicates: { priority: number; providers: string[] }[] = [];
  const priorityMap = new Map<number, string[]>();

  // Group providers by priority
  providers.forEach(provider => {
    if (!priorityMap.has(provider.priority)) {
      priorityMap.set(provider.priority, []);
    }
    const providerList = priorityMap.get(provider.priority);
    if (providerList) {
      providerList.push(provider.providerName);
    }
  });

  // Find duplicates
  priorityMap.forEach((providerNames, priority) => {
    if (providerNames.length > 1) {
      duplicates.push({ priority, providers: providerNames });
      errors.push(`Priority ${priority} is used by multiple providers: ${providerNames.join(', ')}`);
    }
  });

  return {
    isValid: errors.length === 0,
    errors,
    duplicates,
  };
}

/**
 * Auto-adjusts priorities to ensure uniqueness when a provider's priority changes
 */
export function autoAdjustPriorities(
  providers: ProviderPriority[],
  changedProviderId: string,
  newPriority: number
): ProviderPriority[] {
  const result = [...providers];
  const changedProviderIndex = result.findIndex(p => p.providerId === changedProviderId);
  
  if (changedProviderIndex === -1) return result;

  const oldPriority = result[changedProviderIndex].priority;
  
  // If the new priority is the same as the old one, no change needed
  if (oldPriority === newPriority) return result;

  // Update the changed provider's priority
  result[changedProviderIndex].priority = newPriority;

  // Find provider with the conflicting priority
  const conflictingProvider = result.find(
    (p, index) => index !== changedProviderIndex && p.priority === newPriority
  );

  if (conflictingProvider) {
    // Shift the conflicting provider to the old priority
    conflictingProvider.priority = oldPriority;
  }

  return result;
}

/**
 * Normalizes priorities to be sequential starting from 1
 */
export function normalizePriorities(providers: ProviderPriority[]): ProviderPriority[] {
  const sorted = [...providers].sort((a, b) => a.priority - b.priority);
  
  return sorted.map((provider, index) => ({
    ...provider,
    priority: index + 1,
  }));
}

/**
 * Validates priority constraints
 */
export function validatePriorityConstraints(providers: ProviderPriority[]): {
  isValid: boolean;
  errors: string[];
} {
  const errors: string[] = [];

  // Check if at least one provider is enabled
  const enabledProviders = providers.filter(p => p.isEnabled);
  if (enabledProviders.length === 0) {
    errors.push('At least one provider must be enabled');
  }

  // Check priority ranges
  providers.forEach(provider => {
    if (provider.priority < 1) {
      errors.push(`${provider.providerName}: Priority must be at least 1`);
    }
    if (provider.priority > providers.length) {
      errors.push(`${provider.providerName}: Priority cannot exceed ${providers.length}`);
    }
  });

  // Check for gaps in priorities (optional validation)
  const priorities = providers.map(p => p.priority).sort((a, b) => a - b);
  for (let i = 1; i <= providers.length; i++) {
    if (!priorities.includes(i)) {
      errors.push(`Priority ${i} is missing - priorities should be sequential`);
      break;
    }
  }

  return {
    isValid: errors.length === 0,
    errors,
  };
}

/**
 * Calculates optimal priority distribution based on usage statistics
 */
export function suggestOptimalPriorities(providers: Array<ProviderPriority & {
  statistics: {
    usagePercentage: number;
    successRate: number;
    avgResponseTime: number;
  };
}>): ProviderPriority[] {
  // Score providers based on multiple factors
  const scoredProviders = providers.map(provider => {
    const successWeight = 0.4;
    const responseTimeWeight = 0.3;
    const usageWeight = 0.3;

    // Normalize response time (lower is better)
    const normalizedResponseTime = Math.max(0, 100 - (provider.statistics.avgResponseTime / 10));
    
    const score = 
      (provider.statistics.successRate * successWeight) +
      (normalizedResponseTime * responseTimeWeight) +
      (provider.statistics.usagePercentage * usageWeight);

    return {
      ...provider,
      score,
    };
  });

  // Sort by score (descending) and assign priorities
  const sorted = scoredProviders.sort((a, b) => b.score - a.score);
  
  return sorted.map((provider, index) => ({
    providerId: provider.providerId,
    providerName: provider.providerName,
    priority: index + 1,
    isEnabled: provider.isEnabled,
    weight: provider.weight,
  }));
}

/**
 * Gets default configuration for new providers
 */
export function getDefaultProviderConfig(
  existingProviders: ProviderPriority[],
  providerName: string,
  providerType: 'primary' | 'backup' | 'special'
): Partial<ProviderPriority> {
  const highestPriority = existingProviders.length > 0 
    ? Math.max(...existingProviders.map(p => p.priority))
    : 0;

  const defaultPriority = providerType === 'primary' ? 1 : highestPriority + 1;
  const defaultWeight = providerType === 'primary' ? 100 : 50;

  return {
    priority: defaultPriority,
    isEnabled: true,
    weight: defaultWeight,
  };
}

/**
 * Validates provider configuration changes
 */
export function validateProviderChanges(
  originalProviders: ProviderPriority[],
  updatedProviders: ProviderPriority[]
): {
  isValid: boolean;
  warnings: string[];
  errors: string[];
} {
  const warnings: string[] = [];
  const errors: string[] = [];

  // Check for major changes that might affect traffic
  updatedProviders.forEach(updated => {
    const original = originalProviders.find(p => p.providerId === updated.providerId);
    if (!original) return;

    // Warn about disabling high-usage providers
    if (original.isEnabled && !updated.isEnabled) {
      // This would need real usage data
      warnings.push(`Disabling ${updated.providerName} - this may affect traffic distribution`);
    }

    // Warn about major priority changes
    const priorityChange = Math.abs(updated.priority - original.priority);
    if (priorityChange > 3) {
      warnings.push(`Large priority change for ${updated.providerName} (${original.priority} → ${updated.priority})`);
    }
  });

  // Validate unique priorities
  const uniqueValidation = validateUniquePriorities(updatedProviders);
  errors.push(...uniqueValidation.errors);

  // Validate constraints
  const constraintsValidation = validatePriorityConstraints(updatedProviders);
  errors.push(...constraintsValidation.errors);

  return {
    isValid: errors.length === 0,
    warnings,
    errors,
  };
}
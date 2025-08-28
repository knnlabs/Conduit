/**
 * Chat presets for common conversation scenarios.
 * These presets provide optimized parameter combinations for different use cases.
 */

export interface ChatPresetParameters {
  temperature: number;
  topP: number;
  frequencyPenalty: number;
  presencePenalty: number;
}

export interface ChatPreset {
  id: string;
  name: string;
  description: string;
  icon?: string;
  parameters: ChatPresetParameters;
}

/**
 * Predefined chat presets for common use cases.
 * These presets optimize model parameters for specific scenarios.
 */
export const CHAT_PRESETS: ReadonlyArray<ChatPreset> = [
  {
    id: 'balanced',
    name: 'Balanced',
    description: 'Default settings for general conversation',
    icon: 'message-circle',
    parameters: {
      temperature: 0.7,
      topP: 1,
      frequencyPenalty: 0,
      presencePenalty: 0,
    },
  },
  {
    id: 'creative',
    name: 'Creative Writing',
    description: 'Higher creativity for storytelling and brainstorming',
    icon: 'pencil',
    parameters: {
      temperature: 0.9,
      topP: 0.95,
      frequencyPenalty: 0.3,
      presencePenalty: 0.3,
    },
  },
  {
    id: 'code',
    name: 'Code Assistant',
    description: 'Precise responses for programming tasks',
    icon: 'code',
    parameters: {
      temperature: 0.2,
      topP: 0.95,
      frequencyPenalty: 0,
      presencePenalty: 0,
    },
  },
  {
    id: 'analytical',
    name: 'Analytical',
    description: 'Focused and deterministic for analysis tasks',
    icon: 'chart-bar',
    parameters: {
      temperature: 0.1,
      topP: 0.9,
      frequencyPenalty: 0,
      presencePenalty: 0,
    },
  },
  {
    id: 'conversational',
    name: 'Conversational',
    description: 'Natural dialogue with varied responses',
    icon: 'message-circle',
    parameters: {
      temperature: 0.8,
      topP: 0.95,
      frequencyPenalty: 0.5,
      presencePenalty: 0.5,
    },
  },
] as const;

/**
 * Helper function to get a preset by ID.
 * @param id The preset ID
 * @returns The preset if found, undefined otherwise
 */
export function getPresetById(id: string): ChatPreset | undefined {
  return CHAT_PRESETS.find(preset => preset.id === id);
}

/**
 * Helper function to find a preset that matches the given parameters.
 * @param params The parameters to match
 * @returns The matching preset if found, undefined otherwise
 */
export function findMatchingPreset(params: Partial<ChatPresetParameters>): ChatPreset | undefined {
  return CHAT_PRESETS.find(preset => 
    preset.parameters.temperature === params.temperature &&
    preset.parameters.topP === params.topP &&
    preset.parameters.frequencyPenalty === params.frequencyPenalty &&
    preset.parameters.presencePenalty === params.presencePenalty
  );
}

/**
 * Applies a preset's parameters to an existing parameter object.
 * @param preset The preset to apply
 * @param existingParams Optional existing parameters to merge with
 * @returns The merged parameters
 */
export function applyPreset<T extends Partial<ChatPresetParameters>>(
  preset: ChatPreset,
  existingParams?: T
): T & ChatPresetParameters {
  return {
    ...existingParams,
    ...preset.parameters,
  } as T & ChatPresetParameters;
}

/**
 * Gets the default preset (Balanced).
 * @returns The default preset
 */
export function getDefaultPreset(): ChatPreset {
  return CHAT_PRESETS[0]; // Balanced is always the first preset
}

/**
 * Preset categories for grouping related presets.
 */
export enum PresetCategory {
  General = 'general',
  Creative = 'creative',
  Technical = 'technical',
  Analysis = 'analysis',
}

/**
 * Maps presets to categories for organization.
 */
export const PRESET_CATEGORIES: Record<string, PresetCategory> = {
  balanced: PresetCategory.General,
  creative: PresetCategory.Creative,
  code: PresetCategory.Technical,
  analytical: PresetCategory.Analysis,
  conversational: PresetCategory.General,
};

/**
 * Gets presets by category.
 * @param category The category to filter by
 * @returns Array of presets in the specified category
 */
export function getPresetsByCategory(category: PresetCategory): ReadonlyArray<ChatPreset> {
  return CHAT_PRESETS.filter(preset => PRESET_CATEGORIES[preset.id] === category);
}
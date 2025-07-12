/**
 * Model capability constants for the WebUI
 * Defines model capabilities locally to avoid importing SDK on client side
 */

// Model capability enum - mirrors the SDK but defined locally
export enum ModelCapability {
  Chat = 'Chat',
  Completion = 'Completion',
  Embedding = 'Embedding',
  Audio = 'Audio',
  Image = 'Image',
  Video = 'Video',
  FunctionCalling = 'FunctionCalling',
  Vision = 'Vision',
  Code = 'Code',
  Reasoning = 'Reasoning',
  Search = 'Search',
  Json = 'Json',
}

// Display names for capabilities
const CAPABILITY_DISPLAY_NAMES: Record<ModelCapability, string> = {
  [ModelCapability.Chat]: 'Chat',
  [ModelCapability.Completion]: 'Text Completion',
  [ModelCapability.Embedding]: 'Embeddings',
  [ModelCapability.Audio]: 'Audio',
  [ModelCapability.Image]: 'Image Generation',
  [ModelCapability.Video]: 'Video Generation',
  [ModelCapability.FunctionCalling]: 'Function Calling',
  [ModelCapability.Vision]: 'Vision',
  [ModelCapability.Code]: 'Code Generation',
  [ModelCapability.Reasoning]: 'Reasoning',
  [ModelCapability.Search]: 'Web Search',
  [ModelCapability.Json]: 'JSON Mode',
};

export function getCapabilityDisplayName(capability: ModelCapability): string {
  return CAPABILITY_DISPLAY_NAMES[capability] || capability;
}

// Icon configuration for capabilities
export const CAPABILITY_ICONS = {
  [ModelCapability.Chat]: 'IconMessage',
  [ModelCapability.Completion]: 'IconLetterCase',
  [ModelCapability.Embedding]: 'IconVector',
  [ModelCapability.Audio]: 'IconMicrophone',
  [ModelCapability.Image]: 'IconPhoto',
  [ModelCapability.Video]: 'IconVideo',
  [ModelCapability.FunctionCalling]: 'IconFunction',
  [ModelCapability.Vision]: 'IconEye',
  [ModelCapability.Code]: 'IconCode',
  [ModelCapability.Reasoning]: 'IconBrain',
  [ModelCapability.Search]: 'IconSearch',
  [ModelCapability.Json]: 'IconJson',
} as const;

// Badge colors for capabilities  
export const CAPABILITY_COLORS = {
  [ModelCapability.Chat]: 'blue',
  [ModelCapability.Completion]: 'cyan',
  [ModelCapability.Embedding]: 'grape',
  [ModelCapability.Audio]: 'orange',
  [ModelCapability.Image]: 'pink',
  [ModelCapability.Video]: 'red',
  [ModelCapability.FunctionCalling]: 'green',
  [ModelCapability.Vision]: 'indigo',
  [ModelCapability.Code]: 'violet',
  [ModelCapability.Reasoning]: 'yellow',
  [ModelCapability.Search]: 'teal',
  [ModelCapability.Json]: 'gray',
} as const;

// Groups for organizing capabilities in UI
export const CAPABILITY_GROUPS = {
  'Text Processing': [
    ModelCapability.Chat,
    ModelCapability.Completion,
    ModelCapability.Code,
    ModelCapability.Reasoning,
  ],
  'Multimodal': [
    ModelCapability.Vision,
    ModelCapability.Audio,
    ModelCapability.Image,
    ModelCapability.Video,
  ],
  'Advanced Features': [
    ModelCapability.FunctionCalling,
    ModelCapability.Search,
    ModelCapability.Json,
    ModelCapability.Embedding,
  ],
} as const;

// Helper to get capabilities from a comma-separated string
export function parseCapabilities(capabilitiesString?: string | null): ModelCapability[] {
  if (!capabilitiesString) return [];
  
  return capabilitiesString
    .split(',')
    .map(c => c.trim())
    .filter(c => c in ModelCapability)
    .map(c => c as ModelCapability);
}

// Helper to format capabilities for display
export function formatCapabilities(capabilities: ModelCapability[]): string {
  return capabilities.map(getCapabilityDisplayName).join(', ');
}
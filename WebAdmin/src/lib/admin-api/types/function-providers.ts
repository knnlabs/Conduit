// Import and re-export the existing FunctionProviderType enum
import { FunctionProviderType } from '../models/functions';
export { FunctionProviderType };

/**
 * Function provider metadata including display information and capabilities
 */
export interface FunctionProviderMetadata {
  value: FunctionProviderType;
  name: string;
  label: string;
  description?: string;
  supportsAsync?: boolean;
  defaultTimeoutSeconds?: number;
  documentationUrl?: string;
}

/**
 * Function provider configuration registry
 */
export const FUNCTION_PROVIDER_REGISTRY: Record<FunctionProviderType, FunctionProviderMetadata> = {
  [FunctionProviderType.Exa]: {
    value: FunctionProviderType.Exa,
    name: 'Exa',
    label: 'Exa',
    description: 'Exa.ai - Neural and keyword-based web search',
    supportsAsync: true,
    defaultTimeoutSeconds: 30,
    documentationUrl: 'https://docs.exa.ai/'
  },
  [FunctionProviderType.Perplexity]: {
    value: FunctionProviderType.Perplexity,
    name: 'Perplexity',
    label: 'Perplexity',
    description: 'Perplexity AI - Question answering and search',
    supportsAsync: true,
    defaultTimeoutSeconds: 30
  },
  [FunctionProviderType.CustomRAG]: {
    value: FunctionProviderType.CustomRAG,
    name: 'CustomRAG',
    label: 'Custom RAG',
    description: 'Custom RAG implementation',
    supportsAsync: true,
    defaultTimeoutSeconds: 60
  },
  [FunctionProviderType.Tavily]: {
    value: FunctionProviderType.Tavily,
    name: 'Tavily',
    label: 'Tavily',
    description: 'Tavily search API - RAG-optimized search with structured results',
    supportsAsync: true,
    defaultTimeoutSeconds: 30,
    documentationUrl: 'https://docs.tavily.com/'
  },
  [FunctionProviderType.Mcp]: {
    value: FunctionProviderType.Mcp,
    name: 'MCP',
    label: 'MCP Server',
    description: 'Model Context Protocol - expose a remote MCP server\'s tools (Streamable HTTP / SSE)',
    supportsAsync: true,
    defaultTimeoutSeconds: 30,
    documentationUrl: 'https://modelcontextprotocol.io/'
  },
  [FunctionProviderType.Custom]: {
    value: FunctionProviderType.Custom,
    name: 'Custom',
    label: 'Custom',
    description: 'Custom/extensibility provider',
    supportsAsync: true,
    defaultTimeoutSeconds: 30
  }
};

/**
 * Get list of available function providers for dropdown/selection
 */
export function getAvailableFunctionProviders(): FunctionProviderMetadata[] {
  return Object.values(FUNCTION_PROVIDER_REGISTRY);
}

/**
 * Get function provider metadata by type
 */
export function getFunctionProviderMetadata(provider: string | number): FunctionProviderMetadata | undefined {
  const normalizedProvider = normalizeFunctionProviderType(provider);
  if (normalizedProvider !== undefined) {
    return FUNCTION_PROVIDER_REGISTRY[normalizedProvider];
  }

  return undefined;
}

/**
 * Get function provider type name from enum value
 */
export function getFunctionProviderTypeName(value: FunctionProviderType): string {
  const metadata = FUNCTION_PROVIDER_REGISTRY[value];
  return metadata ? metadata.name : String(value);
}

/**
 * Normalize function provider type string to enum value
 */
export function normalizeFunctionProviderType(provider: string | number): FunctionProviderType | undefined {
  if (!provider && provider !== 0) return undefined;

  // If it's already a number, check if it's valid
  if (typeof provider === 'number') {
    const legacyValues = [
      FunctionProviderType.Exa,
      FunctionProviderType.Perplexity,
      FunctionProviderType.CustomRAG,
      FunctionProviderType.Tavily,
      FunctionProviderType.Mcp,
    ];
    return provider === 99 ? FunctionProviderType.Custom : legacyValues[provider - 1];
  }

  // Try to parse as number
  const numValue = parseInt(provider, 10);
  if (!isNaN(numValue)) {
    return normalizeFunctionProviderType(numValue);
  }

  const direct = Object.values(FunctionProviderType).find(
    value => value.toLowerCase() === provider.toLowerCase()
  );
  if (direct) return direct;

  const upperProvider = provider.toUpperCase();
  for (const [key, metadata] of Object.entries(FUNCTION_PROVIDER_REGISTRY)) {
    if (metadata.name.toUpperCase() === upperProvider) {
      return key as FunctionProviderType;
    }
  }

  // Handle special cases
  const specialCases: Record<string, FunctionProviderType> = {
    'exa': FunctionProviderType.Exa,
    'perplexity': FunctionProviderType.Perplexity,
    'customrag': FunctionProviderType.CustomRAG,
    'custom-rag': FunctionProviderType.CustomRAG,
    'tavily': FunctionProviderType.Tavily,
    'mcp': FunctionProviderType.Mcp,
    'custom': FunctionProviderType.Custom
  };

  const lowerProvider = provider.toLowerCase().replace(/[\s_]/g, '');
  if (lowerProvider in specialCases) {
    return specialCases[lowerProvider];
  }

  return undefined;
}

/**
 * Check if a function provider type is valid
 */
export function isValidFunctionProviderType(provider: string | number): boolean {
  return normalizeFunctionProviderType(provider) !== undefined;
}

/**
 * Token estimation utilities for various LLM models
 * Provides token counting, cost estimation, and model family detection
 */

// Type definitions
export interface TokenStats {
  prompt: number;
  completion: number;
  total: number;
}

export interface CostEstimate {
  promptCost: number;
  completionCost: number;
  totalCost: number;
}

export interface ModelPricing {
  promptTokenPrice: number;
  completionTokenPrice: number;
  modelName: string;
  provider: string;
}

export interface EstimatorMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
  images?: ImageDetail[];
}

export interface ImageDetail {
  width?: number;
  height?: number;
  detail: 'low' | 'high' | 'auto';
}

// Model family enum
export enum ModelFamily {
  OpenAI = 'openai',
  Claude = 'claude',
  Gemini = 'gemini',
  Llama = 'llama',
  Generic = 'generic'
}

// Default model pricing configuration
export const DEFAULT_MODEL_PRICING: Record<string, ModelPricing> = {
  // OpenAI GPT models
  'gpt-4o': {
    promptTokenPrice: 0.0000025,
    completionTokenPrice: 0.00001,
    modelName: 'gpt-4o',
    provider: 'openai'
  },
  'gpt-4o-mini': {
    promptTokenPrice: 0.00000015,
    completionTokenPrice: 0.0000006,
    modelName: 'gpt-4o-mini',
    provider: 'openai'
  },
  'gpt-4-turbo': {
    promptTokenPrice: 0.00001,
    completionTokenPrice: 0.00003,
    modelName: 'gpt-4-turbo',
    provider: 'openai'
  },
  'gpt-3.5-turbo': {
    promptTokenPrice: 0.0000005,
    completionTokenPrice: 0.0000015,
    modelName: 'gpt-3.5-turbo',
    provider: 'openai'
  },
  // Claude models
  'claude-3-5-sonnet-20241022': {
    promptTokenPrice: 0.000003,
    completionTokenPrice: 0.000015,
    modelName: 'claude-3-5-sonnet-20241022',
    provider: 'anthropic'
  },
  'claude-3-5-haiku-20241022': {
    promptTokenPrice: 0.0000008,
    completionTokenPrice: 0.000004,
    modelName: 'claude-3-5-haiku-20241022',
    provider: 'anthropic'
  },
  // Generic fallback
  'generic': {
    promptTokenPrice: 0.000001,
    completionTokenPrice: 0.000002,
    modelName: 'generic',
    provider: 'generic'
  }
};

/**
 * Token estimation utility class
 */
export class TokenEstimator {
  /**
   * Get the model family for a given model name
   */
  static getModelFamily(modelName: string): ModelFamily {
    const normalizedModel = modelName.toLowerCase();
    
    if (normalizedModel.includes('gpt') || normalizedModel.includes('openai')) {
      return ModelFamily.OpenAI;
    }
    if (normalizedModel.includes('claude')) {
      return ModelFamily.Claude;
    }
    if (normalizedModel.includes('gemini')) {
      return ModelFamily.Gemini;
    }
    if (normalizedModel.includes('llama')) {
      return ModelFamily.Llama;
    }
    
    return ModelFamily.Generic;
  }

  /**
   * Get pricing information for a model
   */
  static getModelPricing(modelName: string): ModelPricing | undefined {
    // Try exact match first
    if (DEFAULT_MODEL_PRICING[modelName]) {
      return DEFAULT_MODEL_PRICING[modelName];
    }
    
    // Try partial matches
    for (const [key, pricing] of Object.entries(DEFAULT_MODEL_PRICING)) {
      if (modelName.toLowerCase().includes(key.toLowerCase()) || 
          key.toLowerCase().includes(modelName.toLowerCase())) {
        return pricing;
      }
    }
    
    // Return generic pricing as fallback
    return DEFAULT_MODEL_PRICING.generic;
  }

  /**
   * Estimate tokens for a conversation
   */
  static estimateConversationTokens(messages: EstimatorMessage[], _modelFamily: ModelFamily = ModelFamily.Generic): TokenStats {
    let totalPromptTokens = 0;
    
    for (const message of messages) {
      // Basic token estimation - roughly 4 characters per token
      const contentTokens = Math.ceil(message.content.length / 4);
      
      // Add tokens for role and formatting
      const roleTokens = 4;
      
      // Add tokens for images if present
      let imageTokens = 0;
      if (message.images?.length) {
        // Approximate 765 tokens per image for vision models
        imageTokens = message.images.length * 765;
      }
      
      totalPromptTokens += contentTokens + roleTokens + imageTokens;
    }
    
    return {
      prompt: totalPromptTokens,
      completion: 0, // This would be filled in after generation
      total: totalPromptTokens
    };
  }

  /**
   * Estimate cost for token usage
   */
  static estimateCost(tokenStats: TokenStats, pricing: ModelPricing, _modelName: string): CostEstimate {
    const promptCost = tokenStats.prompt * pricing.promptTokenPrice;
    const completionCost = tokenStats.completion * pricing.completionTokenPrice;
    const totalCost = promptCost + completionCost;
    
    return {
      promptCost,
      completionCost,
      totalCost
    };
  }

  /**
   * Estimate tokens for a single message
   */
  static estimateMessageTokens(content: string, _modelFamily: ModelFamily = ModelFamily.Generic): number {
    // Basic estimation - 4 characters per token on average
    return Math.ceil(content.length / 4);
  }

  /**
   * Analyze token usage with detailed breakdown
   */
  static analyzeTokenUsage(tokenStats: TokenStats, maxTokens: number = 128000) {
    const percentage = TokenUtils.calculateTokenPercentage(tokenStats.total, maxTokens);
    const remaining = Math.max(0, maxTokens - tokenStats.total);
    const isApproachingLimit = TokenUtils.isApproachingLimit(tokenStats.total, maxTokens, 0.8);
    const isWarning = percentage > 50;
    const isNearLimit = percentage > 75;
    const isCritical = percentage > 90;
    
    return {
      ...tokenStats,
      percentage,
      remaining,
      maxTokens,
      isApproachingLimit,
      isWarning,
      isNearLimit,
      isCritical
    };
  }
}

/**
 * Token utility functions
 */
export class TokenUtils {
  /**
   * Format token count with appropriate units
   */
  static formatTokenCount(count: number): string {
    if (count < 1000) {
      return count.toString();
    }
    if (count < 1000000) {
      return `${(count / 1000).toFixed(1)}K`;
    }
    return `${(count / 1000000).toFixed(1)}M`;
  }

  /**
   * Calculate token percentage of limit
   */
  static calculateTokenPercentage(used: number, limit: number): number {
    return Math.min((used / limit) * 100, 100);
  }

  /**
   * Check if token usage is approaching limit
   */
  static isApproachingLimit(used: number, limit: number, threshold: number = 0.8): boolean {
    return used / limit >= threshold;
  }

  /**
   * Estimate reading time based on token count
   */
  static estimateReadingTime(tokens: number): string {
    // Approximate reading speed: 200 tokens per minute
    const minutes = tokens / 200;
    if (minutes < 1) {
      return '< 1 min';
    }
    if (minutes < 60) {
      return `${Math.ceil(minutes)} min`;
    }
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = Math.ceil(minutes % 60);
    return `${hours}h ${remainingMinutes}m`;
  }

  /**
   * Format cost with currency symbol
   */
  static formatCost(cost: number): string {
    if (cost < 0.01) {
      return `$${cost.toFixed(6)}`;
    }
    return `$${cost.toFixed(4)}`;
  }

  /**
   * Get color based on usage percentage
   */
  static getUsageColor(percentage: number): string {
    if (percentage < 50) {
      return 'green';
    }
    if (percentage < 75) {
      return 'orange';
    }
    if (percentage < 90) {
      return 'yellow';
    }
    return 'red';
  }
}
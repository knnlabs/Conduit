/**
 * Rough client-side token estimation for the chat UI.
 *
 * This is a display-only heuristic, NOT a tokenizer. It exists so the chat UI
 * can show approximate context-window pressure before a response (and its real
 * `usage` numbers) comes back. The moment actual counts are available they
 * should be used instead - see `TokenCounter`.
 *
 * Deliberately NOT provided here:
 * - Per-model tokenization. Real counts come from the server-side
 *   `ITokenCounter`; anything computed in the browser would be a second,
 *   diverging implementation.
 * - Cost estimation. Pricing lives in ModelCost records served by the Admin
 *   API and is not exposed to the chat page (the Gateway discovery contract
 *   carries no pricing), so the UI omits cost rather than inventing it.
 */

// Type definitions
export interface TokenStats {
  prompt: number;
  completion: number;
  total: number;
}

export interface EstimatorMessage {
  role: "user" | "assistant" | "system";
  content: string;
  images?: ImageDetail[];
}

export interface ImageDetail {
  width?: number;
  height?: number;
  detail: "low" | "high" | "auto";
}

/**
 * Average characters per token across English prose for the tokenizers Conduit
 * routes to. Real counts vary substantially by model, language and content
 * (code and CJK tokenize far denser), which is why every consumer of this
 * module must label its output as an estimate.
 */
export const CHARS_PER_TOKEN_ESTIMATE = 4;

/** Per-message role/formatting scaffolding, in tokens. */
export const TOKENS_PER_MESSAGE_ESTIMATE = 4;

/**
 * Per-image estimate, in tokens. Roughly an OpenAI high-detail 1024x1024 image.
 * Actual cost depends on provider, resolution and detail level.
 */
export const TOKENS_PER_IMAGE_ESTIMATE = 765;

/**
 * Token estimation utility class
 */
export class TokenEstimator {
  /**
   * Roughly estimate the prompt tokens for a conversation.
   *
   * Approximate: character-count heuristic only, no tokenizer involved.
   */
  static estimateConversationTokens(messages: EstimatorMessage[]): TokenStats {
    let totalPromptTokens = 0;

    for (const message of messages) {
      const contentTokens = TokenEstimator.estimateMessageTokens(
        message.content,
      );
      const imageTokens =
        (message.images?.length ?? 0) * TOKENS_PER_IMAGE_ESTIMATE;

      totalPromptTokens +=
        contentTokens + TOKENS_PER_MESSAGE_ESTIMATE + imageTokens;
    }

    return {
      prompt: totalPromptTokens,
      completion: 0, // This would be filled in after generation
      total: totalPromptTokens,
    };
  }

  /**
   * Roughly estimate the tokens in a single piece of text.
   *
   * Approximate: character-count heuristic only, no tokenizer involved.
   */
  static estimateMessageTokens(content: string): number {
    return Math.ceil(content.length / CHARS_PER_TOKEN_ESTIMATE);
  }

  /**
   * Analyze token usage with detailed breakdown
   */
  static analyzeTokenUsage(tokenStats: TokenStats, maxTokens: number = 128000) {
    const percentage = TokenUtils.calculateTokenPercentage(
      tokenStats.total,
      maxTokens,
    );
    const remaining = Math.max(0, maxTokens - tokenStats.total);
    const isApproachingLimit = TokenUtils.isApproachingLimit(
      tokenStats.total,
      maxTokens,
      0.8,
    );
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
      isCritical,
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
  static isApproachingLimit(
    used: number,
    limit: number,
    threshold: number = 0.8,
  ): boolean {
    return used / limit >= threshold;
  }

  /**
   * Estimate reading time based on token count
   */
  static estimateReadingTime(tokens: number): string {
    // Approximate reading speed: 200 tokens per minute
    const minutes = tokens / 200;
    if (minutes < 1) {
      return "< 1 min";
    }
    if (minutes < 60) {
      return `${Math.ceil(minutes)} min`;
    }
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = Math.ceil(minutes % 60);
    return `${hours}h ${remainingMinutes}m`;
  }

  /**
   * Get color based on usage percentage
   */
  static getUsageColor(percentage: number): string {
    if (percentage < 50) {
      return "green";
    }
    if (percentage < 75) {
      return "orange";
    }
    if (percentage < 90) {
      return "yellow";
    }
    return "red";
  }
}

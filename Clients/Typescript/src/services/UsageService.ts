import { BaseService } from './BaseService';
import { ChatCompletionMessage } from '../models/chat';
import { TokenUsage } from '../models/conversation';

export interface TokenEstimate {
  messages: number;
  total: number;
  breakdown: {
    messageId?: string;
    role: string;
    tokens: number;
  }[];
}

export interface ConversationStats {
  conversationId: string;
  totalMessages: number;
  totalTokens: number;
  tokenUsage: TokenUsage;
  averageTPS: number;
  duration: number;
  models: { [model: string]: number };
}

export class UsageService extends BaseService {
  private basePath = '/usage';

  async getTokenCount(messages: ChatCompletionMessage[]): Promise<TokenEstimate> {
    const response = await this.httpClient.post(`${this.basePath}/estimate`, {
      messages
    });
    return response.data;
  }

  async getConversationStats(conversationId: string): Promise<ConversationStats> {
    const response = await this.httpClient.get(
      `${this.basePath}/conversations/${conversationId}`
    );
    return response.data;
  }

  async getUserUsage(
    startDate?: Date,
    endDate?: Date
  ): Promise<{
    totalTokens: number;
    totalCost: number;
    byModel: { [model: string]: TokenUsage & { cost: number } };
    byDay: { date: string; tokens: number; cost: number }[];
  }> {
    const response = await this.httpClient.get(`${this.basePath}/user`, {
      params: {
        start: startDate?.toISOString(),
        end: endDate?.toISOString()
      }
    });
    return response.data;
  }

  async getModelUsage(model: string): Promise<{
    model: string;
    totalRequests: number;
    totalTokens: number;
    averageLatency: number;
    successRate: number;
  }> {
    const response = await this.httpClient.get(`${this.basePath}/models/${model}`);
    return response.data;
  }
}
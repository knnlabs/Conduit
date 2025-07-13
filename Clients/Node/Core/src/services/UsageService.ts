import { BaseService } from './BaseService';
import type { ChatCompletionMessage } from '../models/chat';
import type {
  TokenEstimate,
  ConversationStats,
  UserUsageStats,
  ModelUsageStats
} from '../models/conversation';

export class UsageService extends BaseService {
  async getTokenCount(messages: ChatCompletionMessage[]): Promise<TokenEstimate> {
    const response = await this.client.request({
      method: 'POST',
      path: '/usage/estimate',
      body: { messages }
    });
    return response;
  }

  async getConversationStats(conversationId: string): Promise<ConversationStats> {
    const response = await this.client.request({
      method: 'GET',
      path: `/usage/conversations/${conversationId}`
    });
    return response;
  }

  async getUserUsage(startDate?: Date, endDate?: Date): Promise<UserUsageStats> {
    const response = await this.client.request({
      method: 'GET',
      path: '/usage/user',
      query: {
        start: startDate?.toISOString(),
        end: endDate?.toISOString()
      }
    });
    return response;
  }

  async getModelUsage(model: string): Promise<ModelUsageStats> {
    const response = await this.client.request({
      method: 'GET',
      path: `/usage/models/${model}`
    });
    return response;
  }
}
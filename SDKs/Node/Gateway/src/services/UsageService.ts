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
    return this.clientAdapter.post<TokenEstimate>('/usage/estimate', { messages });
  }

  async getConversationStats(conversationId: string): Promise<ConversationStats> {
    return this.clientAdapter.get<ConversationStats>(`/usage/conversations/${conversationId}`);
  }

  async getUserUsage(startDate?: Date, endDate?: Date): Promise<UserUsageStats> {
    const query = new URLSearchParams();
    if (startDate) query.append('start', startDate.toISOString());
    if (endDate) query.append('end', endDate.toISOString());
    const queryString = query.toString();
    const url = queryString ? `/usage/user?${queryString}` : '/usage/user';
    return this.clientAdapter.get<UserUsageStats>(url);
  }

  async getModelUsage(model: string): Promise<ModelUsageStats> {
    return this.clientAdapter.get<ModelUsageStats>(`/usage/models/${model}`);
  }
}
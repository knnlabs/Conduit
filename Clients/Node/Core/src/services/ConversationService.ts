import { BaseService } from './BaseService';
import type {
  Conversation,
  ConversationListParams,
  ConversationExportParams,
  TokenUsage
} from '../models/conversation';

export class ConversationService extends BaseService {
  async list(params?: ConversationListParams): Promise<Conversation[]> {
    if (!params) {
      return this.clientAdapter.get<Conversation[]>('/conversations');
    }
    const query = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        query.append(key, String(value));
      }
    });
    const queryString = query.toString();
    const url = queryString ? `/conversations?${queryString}` : '/conversations';
    return this.clientAdapter.get<Conversation[]>(url);
  }

  async get(id: string): Promise<Conversation> {
    return this.clientAdapter.get<Conversation>(`/conversations/${id}`);
  }

  async create(conversation: Partial<Conversation>): Promise<Conversation> {
    return this.clientAdapter.post<Conversation>('/conversations', conversation);
  }

  async update(id: string, updates: Partial<Conversation>): Promise<Conversation> {
    return this.clientAdapter.patch<Conversation>(`/conversations/${id}`, updates);
  }

  async delete(id: string): Promise<void> {
    await this.clientAdapter.delete<void>(`/conversations/${id}`);
  }

  async export(id: string, params: ConversationExportParams): Promise<Blob> {
    return this.clientAdapter.post<Blob>(`/conversations/${id}/export`, params, { responseType: 'blob' });
  }

  async search(query: string, limit?: number): Promise<Conversation[]> {
    const params = new URLSearchParams({ q: query });
    if (limit !== undefined) params.append('limit', String(limit));
    return this.clientAdapter.get<Conversation[]>(`/conversations/search?${params.toString()}`);
  }

  async getTokenUsage(id: string): Promise<TokenUsage> {
    return this.clientAdapter.get<TokenUsage>(`/conversations/${id}/usage`);
  }

  async fork(id: string, fromMessageId: string): Promise<Conversation> {
    return this.clientAdapter.post<Conversation>(`/conversations/${id}/fork`, { fromMessageId });
  }
}
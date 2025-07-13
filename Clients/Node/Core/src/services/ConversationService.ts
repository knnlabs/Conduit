import { BaseService } from './BaseService';
import type {
  Conversation,
  ConversationListParams,
  ConversationExportParams,
  TokenUsage
} from '../models/conversation';

export class ConversationService extends BaseService {
  async list(params?: ConversationListParams): Promise<Conversation[]> {
    const response = await this.client.request({
      method: 'GET',
      path: '/conversations',
      query: params
    });
    return response;
  }

  async get(id: string): Promise<Conversation> {
    const response = await this.client.request({
      method: 'GET',
      path: `/conversations/${id}`
    });
    return response;
  }

  async create(conversation: Partial<Conversation>): Promise<Conversation> {
    const response = await this.client.request({
      method: 'POST',
      path: '/conversations',
      body: conversation
    });
    return response;
  }

  async update(id: string, updates: Partial<Conversation>): Promise<Conversation> {
    const response = await this.client.request({
      method: 'PATCH',
      path: `/conversations/${id}`,
      body: updates
    });
    return response;
  }

  async delete(id: string): Promise<void> {
    await this.client.request({
      method: 'DELETE',
      path: `/conversations/${id}`
    });
  }

  async export(id: string, params: ConversationExportParams): Promise<Blob> {
    const response = await this.client.request({
      method: 'POST',
      path: `/conversations/${id}/export`,
      body: params,
      responseType: 'blob'
    });
    return response;
  }

  async search(query: string, limit?: number): Promise<Conversation[]> {
    const response = await this.client.request({
      method: 'GET',
      path: '/conversations/search',
      query: { q: query, limit }
    });
    return response;
  }

  async getTokenUsage(id: string): Promise<TokenUsage> {
    const response = await this.client.request({
      method: 'GET',
      path: `/conversations/${id}/usage`
    });
    return response;
  }

  async fork(id: string, fromMessageId: string): Promise<Conversation> {
    const response = await this.client.request({
      method: 'POST',
      path: `/conversations/${id}/fork`,
      body: { fromMessageId }
    });
    return response;
  }
}
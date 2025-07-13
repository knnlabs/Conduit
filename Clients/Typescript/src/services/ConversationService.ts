import { BaseService } from './BaseService';
import { 
  Conversation, 
  ConversationListParams, 
  ConversationExportFormat,
  MessageEditParams,
  TokenUsage,
  ChatMessage
} from '../models/conversation';

export class ConversationService extends BaseService {
  private basePath = '/conversations';

  async list(params?: ConversationListParams): Promise<Conversation[]> {
    const response = await this.httpClient.get(this.basePath, { params });
    return response.data;
  }

  async get(id: string): Promise<Conversation> {
    const response = await this.httpClient.get(`${this.basePath}/${id}`);
    return response.data;
  }

  async create(conversation: Partial<Conversation>): Promise<Conversation> {
    const response = await this.httpClient.post(this.basePath, conversation);
    return response.data;
  }

  async update(id: string, updates: Partial<Conversation>): Promise<Conversation> {
    const response = await this.httpClient.patch(`${this.basePath}/${id}`, updates);
    return response.data;
  }

  async delete(id: string): Promise<void> {
    await this.httpClient.delete(`${this.basePath}/${id}`);
  }

  async export(id: string, format: ConversationExportFormat): Promise<Blob> {
    const response = await this.httpClient.post(
      `${this.basePath}/${id}/export`,
      format,
      { responseType: 'blob' }
    );
    return response.data;
  }

  async search(query: string, limit?: number): Promise<Conversation[]> {
    const response = await this.httpClient.get(`${this.basePath}/search`, {
      params: { q: query, limit }
    });
    return response.data;
  }

  async getTokenUsage(id: string): Promise<TokenUsage> {
    const response = await this.httpClient.get(`${this.basePath}/${id}/usage`);
    return response.data;
  }

  async fork(id: string, fromMessageId: string): Promise<Conversation> {
    const response = await this.httpClient.post(`${this.basePath}/${id}/fork`, {
      fromMessageId
    });
    return response.data;
  }
}
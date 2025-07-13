import { BaseService } from './BaseService';
import { ChatMessage, MessageEditParams } from '../models/conversation';
import { ChatCompletionCreateParams } from '../models/chat';

export class MessageService extends BaseService {
  private basePath = '/messages';

  async get(messageId: string): Promise<ChatMessage> {
    const response = await this.httpClient.get(`${this.basePath}/${messageId}`);
    return response.data;
  }

  async edit(messageId: string, params: MessageEditParams): Promise<ChatMessage> {
    const response = await this.httpClient.patch(`${this.basePath}/${messageId}`, params);
    return response.data;
  }

  async delete(messageId: string): Promise<void> {
    await this.httpClient.delete(`${this.basePath}/${messageId}`);
  }

  async regenerate(
    messageId: string, 
    options?: Partial<ChatCompletionCreateParams>
  ): Promise<ChatMessage> {
    const response = await this.httpClient.post(
      `${this.basePath}/${messageId}/regenerate`,
      options
    );
    return response.data;
  }

  async copy(messageId: string): Promise<string> {
    const response = await this.httpClient.get(`${this.basePath}/${messageId}/copy`);
    return response.data.content;
  }

  async getBranches(messageId: string): Promise<ChatMessage[]> {
    const response = await this.httpClient.get(`${this.basePath}/${messageId}/branches`);
    return response.data;
  }

  async createBranch(
    messageId: string,
    content: string,
    options?: Partial<ChatCompletionCreateParams>
  ): Promise<ChatMessage> {
    const response = await this.httpClient.post(
      `${this.basePath}/${messageId}/branch`,
      { content, ...options }
    );
    return response.data;
  }
}
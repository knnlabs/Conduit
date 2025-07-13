import { BaseService } from './BaseService';
import type { ChatMessage, MessageEditParams } from '../models/conversation';
import type { ChatCompletionCreateParams } from '../models/chat';

export class MessageService extends BaseService {
  async get(messageId: string): Promise<ChatMessage> {
    const response = await this.client.request({
      method: 'GET',
      path: `/messages/${messageId}`
    });
    return response;
  }

  async edit(messageId: string, params: MessageEditParams): Promise<ChatMessage> {
    const response = await this.client.request({
      method: 'PATCH',
      path: `/messages/${messageId}`,
      body: params
    });
    return response;
  }

  async delete(messageId: string): Promise<void> {
    await this.client.request({
      method: 'DELETE',
      path: `/messages/${messageId}`
    });
  }

  async regenerate(
    messageId: string,
    options?: Partial<ChatCompletionCreateParams>
  ): Promise<ChatMessage> {
    const response = await this.client.request({
      method: 'POST',
      path: `/messages/${messageId}/regenerate`,
      body: options
    });
    return response;
  }

  async copy(messageId: string): Promise<string> {
    const response = await this.client.request({
      method: 'GET',
      path: `/messages/${messageId}/copy`
    });
    return response.content;
  }

  async getBranches(messageId: string): Promise<ChatMessage[]> {
    const response = await this.client.request({
      method: 'GET',
      path: `/messages/${messageId}/branches`
    });
    return response;
  }

  async createBranch(
    messageId: string,
    content: string,
    options?: Partial<ChatCompletionCreateParams>
  ): Promise<ChatMessage> {
    const response = await this.client.request({
      method: 'POST',
      path: `/messages/${messageId}/branch`,
      body: { content, ...options }
    });
    return response;
  }
}
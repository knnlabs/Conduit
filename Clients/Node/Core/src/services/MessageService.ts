import { BaseService } from './BaseService';
import type { ChatMessage, MessageEditParams } from '../models/conversation';
import type { ChatCompletionCreateParams } from '../models/chat';

export class MessageService extends BaseService {
  async get(messageId: string): Promise<ChatMessage> {
    return this.clientAdapter.get<ChatMessage>(`/messages/${messageId}`);
  }

  async edit(messageId: string, params: MessageEditParams): Promise<ChatMessage> {
    return this.clientAdapter.patch<ChatMessage>(`/messages/${messageId}`, params);
  }

  async delete(messageId: string): Promise<void> {
    await this.clientAdapter.delete<void>(`/messages/${messageId}`);
  }

  async regenerate(
    messageId: string,
    options?: Partial<ChatCompletionCreateParams>
  ): Promise<ChatMessage> {
    return this.clientAdapter.post<ChatMessage>(`/messages/${messageId}/regenerate`, options);
  }

  async copy(messageId: string): Promise<string> {
    const response = await this.clientAdapter.get<{ content: string }>(`/messages/${messageId}/copy`);
    return response.content;
  }

  async getBranches(messageId: string): Promise<ChatMessage[]> {
    return this.clientAdapter.get<ChatMessage[]>(`/messages/${messageId}/branches`);
  }

  async createBranch(
    messageId: string,
    content: string,
    options?: Partial<ChatCompletionCreateParams>
  ): Promise<ChatMessage> {
    return this.clientAdapter.post<ChatMessage>(`/messages/${messageId}/branch`, { content, ...options });
  }
}
import { BaseClient } from './BaseClient';
import type { ClientConfig } from './types';
import { ChatService } from '../services/ChatService';
import { ModelsService } from '../services/ModelsService';
import { ImagesService } from '../services/ImagesService';
import { VideosService } from '../services/VideosService';

export class ConduitCoreClient extends BaseClient {
  public readonly chat: {
    completions: ChatService;
  };
  public readonly images: ImagesService;
  public readonly videos: VideosService;
  public readonly models: ModelsService;

  constructor(config: ClientConfig) {
    super(config);

    this.chat = {
      completions: new ChatService(this),
    };

    this.images = new ImagesService(this);
    this.videos = new VideosService(this);
    this.models = new ModelsService(this);
  }

  static fromApiKey(apiKey: string, baseURL?: string): ConduitCoreClient {
    return new ConduitCoreClient({
      apiKey,
      baseURL,
    });
  }
}
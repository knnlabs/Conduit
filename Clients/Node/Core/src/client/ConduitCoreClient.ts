import { BaseClient } from './BaseClient';
import type { ClientConfig } from './types';
import { ChatService } from '../services/ChatService';
import { ModelsService } from '../services/ModelsService';
import { ImagesService } from '../services/ImagesService';
import { VideosService } from '../services/VideosService';
import { TasksService } from '../services/TasksService';
import { AudioService } from '../services/AudioService';
import { BatchOperationsService } from '../services/BatchOperationsService';
import { HealthService } from '../services/HealthService';
import { MetricsService } from '../services/MetricsService';
import { DiscoveryService } from '../services/DiscoveryService';
import { ProviderModelsService } from '../services/ProviderModelsService';
import { SignalRService } from '../services/SignalRService';

export class ConduitCoreClient extends BaseClient {
  public readonly chat: {
    completions: ChatService;
  };
  public readonly audio: AudioService;
  public readonly images: ImagesService;
  public readonly videos: VideosService;
  public readonly models: ModelsService;
  public readonly tasks: TasksService;
  public readonly batchOperations: BatchOperationsService;
  public readonly health: HealthService;
  public readonly metrics: MetricsService;
  public readonly discovery: DiscoveryService;
  public readonly providerModels: ProviderModelsService;
  public readonly signalr: SignalRService;

  constructor(config: ClientConfig) {
    super(config);

    this.chat = {
      completions: new ChatService(this),
    };

    this.audio = new AudioService(this);
    this.images = new ImagesService(this);
    this.videos = new VideosService(this);
    this.models = new ModelsService(this);
    this.tasks = new TasksService(this);
    this.batchOperations = new BatchOperationsService(this);
    this.health = new HealthService(this);
    this.metrics = new MetricsService(this);
    this.discovery = new DiscoveryService(this);
    this.providerModels = new ProviderModelsService(this);
    this.signalr = new SignalRService(config.baseURL || 'http://localhost:5000', config.apiKey);
  }

  static fromApiKey(apiKey: string, baseURL?: string): ConduitCoreClient {
    return new ConduitCoreClient({
      apiKey,
      baseURL,
    });
  }
}
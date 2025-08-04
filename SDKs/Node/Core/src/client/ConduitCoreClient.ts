import { FetchBasedClient } from './FetchBasedClient';
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
import { EmbeddingsService } from '../services/EmbeddingsService';
import { NotificationsService } from '../services/NotificationsService';
import { ConnectionService } from '../services/ConnectionService';
import { ConversationService } from '../services/ConversationService';
import { MessageService } from '../services/MessageService';
import { UsageService } from '../services/UsageService';
import type { VideoGenerationHubClient } from '../signalr/VideoGenerationHubClient';

export class ConduitCoreClient extends FetchBasedClient {
  public readonly chat: ChatService;
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
  public readonly embeddings: EmbeddingsService;
  public readonly notifications: NotificationsService;
  public readonly connection: ConnectionService;
  public readonly conversations: ConversationService;
  public readonly messages: MessageService;
  public readonly usage: UsageService;

  constructor(config: ClientConfig) {
    super(config);

    this.chat = new ChatService(this);

    this.audio = new AudioService(this);
    this.images = new ImagesService(this);
    
    // Initialize SignalR with configuration first
    const signalRConfig = config.signalR ?? {};
    this.signalr = new SignalRService(
      config.baseURL ?? 'http://localhost:5000', 
      config.apiKey
    );
    
    // Import VideoGenerationHubClient dynamically to avoid circular dependencies
    import('../signalr/VideoGenerationHubClient').then(({ VideoGenerationHubClient }) => {
      const videoHubClient = new VideoGenerationHubClient(
        config.baseURL ?? 'http://localhost:5000',
        config.apiKey
      );
      // Update videos service with SignalR connections
      if (this.videos instanceof VideosService) {
        // TypeScript doesn't know about these internal properties, but they exist
        const videosService = this.videos as VideosService & {
          signalRService?: SignalRService;
          videoHubClient?: VideoGenerationHubClient;
        };
        videosService.signalRService = this.signalr;
        videosService.videoHubClient = videoHubClient;
      }
    }).catch(error => {
      if (config.debug) {
        console.error('Failed to initialize VideoGenerationHubClient:', error);
      }
    });
    
    this.videos = new VideosService(this, this.signalr);
    this.models = new ModelsService(this);
    this.tasks = new TasksService(this);
    this.batchOperations = new BatchOperationsService(this);
    this.health = new HealthService(this);
    this.metrics = new MetricsService(this);
    this.discovery = new DiscoveryService(this);
    this.providerModels = new ProviderModelsService(this);
    
    this.embeddings = new EmbeddingsService(this);
    this.notifications = new NotificationsService(this.signalr);
    
    // Initialize connection service
    this.connection = new ConnectionService(this);
    this.connection.initializeSignalR(this.signalr);
    
    // Initialize conversation management services
    this.conversations = new ConversationService(this);
    this.messages = new MessageService(this);
    this.usage = new UsageService(this);
    
    // Auto-connect SignalR if enabled
    if (signalRConfig.enabled !== false && signalRConfig.autoConnect !== false) {
      this.signalr.startAllConnections().catch((error: Error) => {
        if (config.debug) {
          console.error('Failed to connect to SignalR:', error);
        }
      });
    }
  }

  static fromApiKey(apiKey: string, baseURL?: string): ConduitCoreClient {
    return new ConduitCoreClient({
      apiKey,
      baseURL,
    });
  }
}
/**
 * Type declarations for linked SDK packages
 * This helps ESLint understand the types from locally linked packages
 */

declare module '@knn_labs/conduit-admin-client' {
  interface RequestLogParams {
    page?: number;
    pageSize?: number;
    startDate?: string;
    endDate?: string;
    virtualKeyId?: string;
    provider?: string;
    model?: string;
    statusCode?: number;
  }
  
  interface RequestLog {
    id: string;
    timestamp: string;
    inputTokens: number;
    outputTokens: number;
    status: string;
    duration: number;
    virtualKeyName?: string;
    virtualKeyId?: string;
    provider: string;
    model: string;
    cost?: number;
    errorMessage?: string;
    ipAddress?: string;
    userAgent?: string;
  }
  
  interface RequestLogResponse {
    items: RequestLog[];
    totalCount: number;
    page: number;
    pageSize: number;
  }
  
  interface SystemInfo {
    version: string;
    environment: string;
    uptime: number;
    database?: {
      provider: string;
      version: string;
      migrationStatus: string;
      connectionStatus: string;
      [key: string]: unknown;
    };
    features?: {
      webSockets: boolean;
      batch: boolean;
      images: boolean;
      audio: boolean;
      [key: string]: unknown;
    };
    buildDate?: string;
    runtime?: string;
    [key: string]: unknown;
  }
  
  interface AdminConfig {
    baseUrl: string;
    masterKey: string;
    timeout?: number;
    retries?: number;
  }
  
  interface ProvidersService {
    list(page: number, pageSize: number): Promise<{ items: ProviderCredentialDto[] }>;
    create(data: CreateProviderCredentialDto): Promise<ProviderCredentialDto>;
    testConfig(config: {
      providerType: ProviderType;
      apiKey: string;
      baseUrl?: string;
      organizationId?: string;
      additionalConfig?: ProviderSettings;
    }): Promise<ProviderConnectionTestResultDto>;
    getById(id: number): Promise<ProviderCredentialDto>;
  }
  
  interface ProviderModelsService {
    getModelDetails(providerName: string, modelId: string): Promise<unknown>;
    refreshProviderModels(providerName: string): Promise<unknown[]>;
  }
  
  export class ConduitAdminClient {
    constructor(config: AdminConfig);
    analytics: {
      getRequestLogs(params: RequestLogParams): Promise<RequestLogResponse>;
    };
    system: {
      getSystemInfo(): Promise<SystemInfo>;
      getWebUIVirtualKey(): Promise<string>;
      health(): Promise<{ status: string; checks: Record<string, { status: string }> }>;
    };
    providers: ProvidersService;
    virtualKeys: {
      list(page: number, pageSize: number): Promise<{ items: Array<{ id: number; [key: string]: unknown }> }>;
      [key: string]: unknown;
    };
    modelMappings: {
      list(filters?: unknown): Promise<{ items: Array<{ id: number; providerId: string; modelId: string; [key: string]: unknown }> }>;
      update(id: number, data: unknown): Promise<unknown>;
      create(data: unknown): Promise<unknown>;
      [key: string]: unknown;
    };
    providerModels: ProviderModelsService;
    settings: {
      list(): Promise<Array<{ key: string; value: string; [key: string]: unknown }>>;
      [key: string]: unknown;
    };
    ipFilter: {
      getById(id: number): Promise<IpFilterDto>;
      update(id: number, data: UpdateIpFilterDto): Promise<void>;
      deleteById(id: number): Promise<void>;
      list(filters?: unknown): Promise<{ items: IpFilterDto[] }>;
      create(data: unknown): Promise<IpFilterDto>;
      bulkDelete(ids: number[]): Promise<unknown>;
      bulkUpdate(operation: string, ids: number[]): Promise<IpFilterDto[]>;
      bulkCreate(data: unknown[]): Promise<unknown>;
      testFilter(ip: string): Promise<unknown>;
      importFromCsv(file: unknown): Promise<unknown>;
      import(rules: IpFilterImport[]): Promise<{ imported: number; failed: number }>;
      exportToCsv(): Promise<unknown>;
      export(format: string): Promise<Blob>;
      [key: string]: unknown;
    };
    modelCosts: unknown;
    providerHealth: unknown;
    notifications: unknown;
    security: unknown;
    configuration: unknown;
    monitoring: unknown;
  }
  
  export interface ProviderCredentialDto {
    id: number;
    providerType: ProviderType;
    apiKey?: string;
    apiBase?: string;
    organization?: string;
    isEnabled: boolean;
    createdAt: string;
    updatedAt: string;
  }
  
  export interface CreateProviderCredentialDto {
    providerType: ProviderType;
    apiKey?: string;
    apiBase?: string;
    organization?: string;
    isEnabled?: boolean;
  }
  
  export interface UpdateProviderCredentialDto {
    apiKey?: string;
    apiBase?: string;
    organization?: string;
    isEnabled?: boolean;
  }
  
  export interface ProviderConnectionTestResultDto {
    success: boolean;
    message: string;
    errorDetails?: string;
    providerType: ProviderType;
    modelsAvailable?: string[];
    responseTimeMs?: number;
    timestamp?: string;
  }
  
  export interface ProviderSettings {
    [key: string]: string | number | boolean | null | undefined;
  }
  
  export enum ProviderType {
    OpenAI = 1,
    Anthropic = 2,
    AzureOpenAI = 3,
    Gemini = 4,
    VertexAI = 5,
    Cohere = 6,
    Mistral = 7,
    Groq = 8,
    Ollama = 9,
    Replicate = 10,
    Fireworks = 11,
    Bedrock = 12,
    HuggingFace = 13,
    SageMaker = 14,
    OpenRouter = 15,
    OpenAICompatible = 16,
    MiniMax = 17,
    Ultravox = 18,
    ElevenLabs = 19,
    GoogleCloud = 20,
    Cerebras = 21
  }
  
  export interface ModelProviderMappingDto {
    id: number;
    modelId: string;
    providerId: string;
    providerModelId: string;
    priority?: number;
    isEnabled: boolean;
    supportsVision?: boolean;
    supportsImageGeneration?: boolean;
    supportsAudioTranscription?: boolean;
    supportsTextToSpeech?: boolean;
    supportsRealtimeAudio?: boolean;
    supportsFunctionCalling?: boolean;
    supportsStreaming?: boolean;
    supportsVideoGeneration?: boolean;
    supportsEmbeddings?: boolean;
    maxContextLength?: number;
    maxOutputTokens?: number;
    isDefault?: boolean;
    [key: string]: unknown;
  }
  
  export interface CreateModelProviderMappingDto {
    modelId: string;
    providerId: string;
    providerModelId: string;
    priority?: number;
    isEnabled?: boolean;
    supportsVision?: boolean;
    supportsImageGeneration?: boolean;
    supportsAudioTranscription?: boolean;
    supportsTextToSpeech?: boolean;
    supportsRealtimeAudio?: boolean;
    supportsFunctionCalling?: boolean;
    supportsStreaming?: boolean;
    supportsVideoGeneration?: boolean;
    supportsEmbeddings?: boolean;
    maxContextLength?: number;
    maxOutputTokens?: number;
    isDefault?: boolean;
  }
  
  export type UpdateModelProviderMappingDto = CreateModelProviderMappingDto;
  
  export interface IpFilterDto {
    id: number;
    name: string;
    ipAddressOrCidr: string;
    filterType: 'whitelist' | 'blacklist';
    isEnabled: boolean;
    description?: string;
    createdAt: string;
    updatedAt: string;
    lastMatchedAt?: string;
    matchCount?: number;
    expiresAt?: string;
    createdBy?: string;
    lastModifiedBy?: string;
    blockedCount?: number;
  }
  
  export interface CreateIpFilterDto {
    name: string;
    ipAddressOrCidr: string;
    filterType: 'whitelist' | 'blacklist';
    isEnabled?: boolean;
    description?: string;
  }
  
  export interface UpdateIpFilterDto {
    id: number;
    name?: string;
    ipAddressOrCidr?: string;
    filterType?: 'whitelist' | 'blacklist';
    isEnabled?: boolean;
    description?: string;
  }
  
  export interface IpFilterImport {
    ipAddress: string;
    action: 'allow' | 'block';
    description?: string;
  }
  
  // Additional exports
  export function getProviderTypeFromDto(dto: { providerType?: number; providerName?: string }): ProviderType;
  export function getProviderDisplayName(providerType: ProviderType): string;
  export function providerTypeToName(providerType: ProviderType): string;
  
  export interface SystemInfoDto {
    version: string;
    buildDate: string;
    environment: string;
    uptime: number;
    systemTime: string;
    features: {
      ipFiltering: boolean;
      providerHealth: boolean;
      costTracking: boolean;
      audioSupport: boolean;
      webSockets?: boolean;
      batch?: boolean;
      images?: boolean;
      audio?: boolean;
    };
    runtime: {
      dotnetVersion: string;
      os: string;
      architecture: string;
    };
    database: {
      provider: string;
      connectionString?: string;
      isConnected: boolean;
      pendingMigrations?: string[];
      version?: string;
      migrationStatus?: string;
      connectionStatus?: string;
    };
  }
}

declare module '@knn_labs/conduit-core-client' {
  interface CoreConfig {
    apiKey: string;
    baseURL: string;
    signalR?: { enabled: boolean };
  }
  
  export class ConduitCoreClient {
    constructor(config: CoreConfig);
    // Add methods as needed
  }
}
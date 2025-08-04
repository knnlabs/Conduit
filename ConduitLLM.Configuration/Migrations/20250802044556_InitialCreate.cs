using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CacheConfigurationAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldConfigJson = table.Column<string>(type: "text", nullable: true),
                    NewConfigJson = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangeSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheConfigurationAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CacheConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultTtlSeconds = table.Column<int>(type: "integer", nullable: true),
                    MaxTtlSeconds = table.Column<int>(type: "integer", nullable: true),
                    MaxEntries = table.Column<long>(type: "bigint", nullable: true),
                    MaxMemoryBytes = table.Column<long>(type: "bigint", nullable: true),
                    EvictionPolicy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UseMemoryCache = table.Column<bool>(type: "boolean", nullable: false),
                    UseDistributedCache = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCompression = table.Column<bool>(type: "boolean", nullable: false),
                    CompressionThresholdBytes = table.Column<long>(type: "bigint", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    EnableDetailedStats = table.Column<bool>(type: "boolean", nullable: false),
                    ExtendedConfig = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IpFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FilterType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IpAddressOrCidr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CostName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    InputTokenCost = table.Column<decimal>(type: "numeric(18,10)", nullable: false),
                    OutputTokenCost = table.Column<decimal>(type: "numeric(18,10)", nullable: false),
                    EmbeddingTokenCost = table.Column<decimal>(type: "numeric(18,10)", nullable: true),
                    ImageCostPerImage = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModelType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    AudioCostPerMinute = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AudioCostPerKCharacters = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AudioInputCostPerMinute = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AudioOutputCostPerMinute = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    VideoCostPerSecond = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    VideoResolutionMultipliers = table.Column<string>(type: "text", nullable: true),
                    BatchProcessingMultiplier = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    SupportsBatchProcessing = table.Column<bool>(type: "boolean", nullable: false),
                    ImageQualityMultipliers = table.Column<string>(type: "text", nullable: true),
                    CachedInputTokenCost = table.Column<decimal>(type: "numeric(18,10)", nullable: true),
                    CachedInputWriteCost = table.Column<decimal>(type: "numeric(18,10)", nullable: true),
                    CostPerSearchUnit = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CostPerInferenceStep = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    DefaultInferenceSteps = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RouterConfigEntity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DefaultRoutingStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    RetryBaseDelayMs = table.Column<int>(type: "integer", nullable: false),
                    RetryMaxDelayMs = table.Column<int>(type: "integer", nullable: false),
                    FallbacksEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouterConfigEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualKeyGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalGroupId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GroupName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(19,8)", nullable: false),
                    LifetimeCreditsAdded = table.Column<decimal>(type: "numeric(19,8)", nullable: false),
                    LifetimeSpent = table.Column<decimal>(type: "numeric(19,8)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeyGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CostUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CostPerUnit = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    MinimumCharge = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    AdditionalFactors = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioCosts_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    TranscriptionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultTranscriptionModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TextToSpeechEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultTTSModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefaultTTSVoice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RealtimeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultRealtimeModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RealtimeEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomSettings = table.Column<string>(type: "text", nullable: true),
                    RoutingPriority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioProviderConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioProviderConfigs_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioUsageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VirtualKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    CharacterCount = table.Column<int>(type: "integer", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Voice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioUsageLogs_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelProviderMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelAlias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderModelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxContextTokens = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupportsVision = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsAudioTranscription = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsTextToSpeech = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsRealtimeAudio = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsImageGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVideoGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsEmbeddings = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsChat = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsFunctionCalling = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    TokenizerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SupportedVoices = table.Column<string>(type: "text", nullable: true),
                    SupportedLanguages = table.Column<string>(type: "text", nullable: true),
                    SupportedFormats = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultCapabilityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelProviderMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelProviderMappings_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHealthConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    MonitoringEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CheckIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    ConsecutiveFailuresThreshold = table.Column<int>(type: "integer", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CustomEndpointUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastCheckedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderHealthConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderHealthConfigurations_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHealthRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    StatusMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseTimeMs = table.Column<double>(type: "double precision", nullable: false),
                    ErrorCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ErrorDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EndpointUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderHealthRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderHealthRecords_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderKeyCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    ProviderAccountGroup = table.Column<short>(type: "smallint", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    BaseUrl = table.Column<string>(type: "text", nullable: true),
                    Organization = table.Column<string>(type: "text", nullable: true),
                    KeyName = table.Column<string>(type: "text", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderKeyCredentials", x => x.Id);
                    table.CheckConstraint("CK_ProviderKeyCredential_AccountGroupRange", "\"ProviderAccountGroup\" >= 0 AND \"ProviderAccountGroup\" <= 32");
                    table.CheckConstraint("CK_ProviderKeyCredential_PrimaryMustBeEnabled", "\"IsPrimary\" = false OR \"IsEnabled\" = true");
                    table.ForeignKey(
                        name: "FK_ProviderKeyCredentials_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FallbackConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryModelDeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouterConfigId = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FallbackConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FallbackConfigurations_RouterConfigEntity_RouterConfigId",
                        column: x => x.RouterConfigId,
                        principalTable: "RouterConfigEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    HealthCheckEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RPM = table.Column<int>(type: "integer", nullable: true),
                    TPM = table.Column<int>(type: "integer", nullable: true),
                    InputTokenCostPer1K = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    OutputTokenCostPer1K = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    RouterConfigId = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeploymentName = table.Column<string>(type: "text", nullable: false),
                    SupportsEmbeddings = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelDeployments_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelDeployments_RouterConfigEntity_RouterConfigId",
                        column: x => x.RouterConfigId,
                        principalTable: "RouterConfigEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VirtualKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KeyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    VirtualKeyGroupId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    AllowedModels = table.Column<string>(type: "text", nullable: true),
                    RateLimitRpm = table.Column<int>(type: "integer", nullable: true),
                    RateLimitRpd = table.Column<int>(type: "integer", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualKeys_VirtualKeyGroups_VirtualKeyGroupId",
                        column: x => x.VirtualKeyGroupId,
                        principalTable: "VirtualKeyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelCostMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelCostId = table.Column<int>(type: "integer", nullable: false),
                    ModelProviderMappingId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCostMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelCostMappings_ModelCosts_ModelCostId",
                        column: x => x.ModelCostId,
                        principalTable: "ModelCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelCostMappings_ModelProviderMappings_ModelProviderMappin~",
                        column: x => x.ModelProviderMappingId,
                        principalTable: "ModelProviderMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FallbackModelMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FallbackConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelDeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    SourceModelName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FallbackModelMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FallbackModelMappings_FallbackConfigurations_FallbackConfig~",
                        column: x => x.FallbackConfigurationId,
                        principalTable: "FallbackConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AsyncTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    ProgressMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Result = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeasedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeaseExpiryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    IsRetryable = table.Column<bool>(type: "boolean", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsyncTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsyncTasks_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BatchOperationHistory",
                columns: table => new
                {
                    OperationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OperationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    ItemsPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    ResultSummary = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CheckpointData = table.Column<string>(type: "text", nullable: true),
                    CanResume = table.Column<bool>(type: "boolean", nullable: false),
                    LastProcessedIndex = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchOperationHistory", x => x.OperationId);
                    table.ForeignKey(
                        name: "FK_BatchOperationHistory_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaLifecycleRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    MediaType = table.Column<string>(type: "text", nullable: false),
                    MediaUrl = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    GeneratedByModel = table.Column<string>(type: "text", nullable: false),
                    GenerationPrompt = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaLifecycleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaLifecycleRecords_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    MediaType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Prompt = table.Column<string>(type: "text", nullable: true),
                    StorageUrl = table.Column<string>(type: "text", nullable: true),
                    PublicUrl = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaRecords_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    ResponseTimeMs = table.Column<double>(type: "double precision", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientIp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestLogs_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VirtualKeySpendHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeySpendHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualKeySpendHistory_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_Archival",
                table: "AsyncTasks",
                columns: new[] { "IsArchived", "CompletedAt", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_Cleanup",
                table: "AsyncTasks",
                columns: new[] { "IsArchived", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_CreatedAt",
                table: "AsyncTasks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_IsArchived",
                table: "AsyncTasks",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_State",
                table: "AsyncTasks",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_Type",
                table: "AsyncTasks",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_VirtualKeyId",
                table: "AsyncTasks",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_AsyncTasks_VirtualKeyId_CreatedAt",
                table: "AsyncTasks",
                columns: new[] { "VirtualKeyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioCosts_EffectiveFrom_EffectiveTo",
                table: "AudioCosts",
                columns: new[] { "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioCosts_ProviderId_OperationType_Model_IsActive",
                table: "AudioCosts",
                columns: new[] { "ProviderId", "OperationType", "Model", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioProviderConfigs_ProviderId",
                table: "AudioProviderConfigs",
                column: "ProviderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioUsageLogs_ProviderId_OperationType",
                table: "AudioUsageLogs",
                columns: new[] { "ProviderId", "OperationType" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioUsageLogs_SessionId",
                table: "AudioUsageLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioUsageLogs_Timestamp",
                table: "AudioUsageLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AudioUsageLogs_VirtualKey",
                table: "AudioUsageLogs",
                column: "VirtualKey");

            migrationBuilder.CreateIndex(
                name: "IX_BatchOperationHistory_OperationType",
                table: "BatchOperationHistory",
                column: "OperationType");

            migrationBuilder.CreateIndex(
                name: "IX_BatchOperationHistory_OperationType_Status_StartedAt",
                table: "BatchOperationHistory",
                columns: new[] { "OperationType", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BatchOperationHistory_StartedAt",
                table: "BatchOperationHistory",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BatchOperationHistory_Status",
                table: "BatchOperationHistory",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BatchOperationHistory_VirtualKeyId",
                table: "BatchOperationHistory",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchOperationHistory_VirtualKeyId_StartedAt",
                table: "BatchOperationHistory",
                columns: new[] { "VirtualKeyId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CacheConfigurationAudits_ChangedAt",
                table: "CacheConfigurationAudits",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CacheConfigurationAudits_ChangedBy",
                table: "CacheConfigurationAudits",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CacheConfigurationAudits_Region",
                table: "CacheConfigurationAudits",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_CacheConfigurationAudits_Region_ChangedAt",
                table: "CacheConfigurationAudits",
                columns: new[] { "Region", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CacheConfigurations_Region",
                table: "CacheConfigurations",
                column: "Region",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_CacheConfigurations_Region_IsActive",
                table: "CacheConfigurations",
                columns: new[] { "Region", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CacheConfigurations_UpdatedAt",
                table: "CacheConfigurations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackConfigurations_PrimaryModelDeploymentId",
                table: "FallbackConfigurations",
                column: "PrimaryModelDeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackConfigurations_RouterConfigId",
                table: "FallbackConfigurations",
                column: "RouterConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackModelMappings_FallbackConfigurationId_ModelDeployme~",
                table: "FallbackModelMappings",
                columns: new[] { "FallbackConfigurationId", "ModelDeploymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FallbackModelMappings_FallbackConfigurationId_Order",
                table: "FallbackModelMappings",
                columns: new[] { "FallbackConfigurationId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalSettings_Key",
                table: "GlobalSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpFilters_FilterType_IpAddressOrCidr",
                table: "IpFilters",
                columns: new[] { "FilterType", "IpAddressOrCidr" });

            migrationBuilder.CreateIndex(
                name: "IX_IpFilters_IsEnabled",
                table: "IpFilters",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_CreatedAt",
                table: "MediaLifecycleRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_ExpiresAt",
                table: "MediaLifecycleRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_ExpiresAt_IsDeleted",
                table: "MediaLifecycleRecords",
                columns: new[] { "ExpiresAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_StorageKey",
                table: "MediaLifecycleRecords",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_VirtualKeyId",
                table: "MediaLifecycleRecords",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLifecycleRecords_VirtualKeyId_IsDeleted",
                table: "MediaLifecycleRecords",
                columns: new[] { "VirtualKeyId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_CreatedAt",
                table: "MediaRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_ExpiresAt",
                table: "MediaRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_StorageKey",
                table: "MediaRecords",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_VirtualKeyId",
                table: "MediaRecords",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRecords_VirtualKeyId_CreatedAt",
                table: "MediaRecords",
                columns: new[] { "VirtualKeyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCostMappings_ModelCostId_ModelProviderMappingId",
                table: "ModelCostMappings",
                columns: new[] { "ModelCostId", "ModelProviderMappingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelCostMappings_ModelProviderMappingId",
                table: "ModelCostMappings",
                column: "ModelProviderMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCosts_CostName",
                table: "ModelCosts",
                column: "CostName");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_IsEnabled",
                table: "ModelDeployments",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_IsHealthy",
                table: "ModelDeployments",
                column: "IsHealthy");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_ModelName",
                table: "ModelDeployments",
                column: "ModelName");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_ProviderId",
                table: "ModelDeployments",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_RouterConfigId",
                table: "ModelDeployments",
                column: "RouterConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMappings_ModelAlias_ProviderId",
                table: "ModelProviderMappings",
                columns: new[] { "ModelAlias", "ProviderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMappings_ProviderId",
                table: "ModelProviderMappings",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_VirtualKeyId",
                table: "Notifications",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHealthConfigurations_ProviderId",
                table: "ProviderHealthConfigurations",
                column: "ProviderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHealthRecords_IsOnline",
                table: "ProviderHealthRecords",
                column: "IsOnline");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHealthRecords_ProviderId_TimestampUtc",
                table: "ProviderHealthRecords",
                columns: new[] { "ProviderId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeyCredential_OnePrimaryPerProvider",
                table: "ProviderKeyCredentials",
                columns: new[] { "ProviderId", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeyCredential_ProviderId",
                table: "ProviderKeyCredentials",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeyCredential_UniqueApiKeyPerProvider",
                table: "ProviderKeyCredentials",
                columns: new[] { "ProviderId", "ApiKey" },
                unique: true,
                filter: "\"ApiKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Providers_ProviderType",
                table: "Providers",
                column: "ProviderType");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_VirtualKeyId",
                table: "RequestLogs",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_RouterConfigEntity_LastUpdated",
                table: "RouterConfigEntity",
                column: "LastUpdated");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroups_ExternalGroupId",
                table: "VirtualKeyGroups",
                column: "ExternalGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeys_KeyHash",
                table: "VirtualKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeys_VirtualKeyGroupId",
                table: "VirtualKeys",
                column: "VirtualKeyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeySpendHistory_VirtualKeyId",
                table: "VirtualKeySpendHistory",
                column: "VirtualKeyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsyncTasks");

            migrationBuilder.DropTable(
                name: "AudioCosts");

            migrationBuilder.DropTable(
                name: "AudioProviderConfigs");

            migrationBuilder.DropTable(
                name: "AudioUsageLogs");

            migrationBuilder.DropTable(
                name: "BatchOperationHistory");

            migrationBuilder.DropTable(
                name: "CacheConfigurationAudits");

            migrationBuilder.DropTable(
                name: "CacheConfigurations");

            migrationBuilder.DropTable(
                name: "FallbackModelMappings");

            migrationBuilder.DropTable(
                name: "GlobalSettings");

            migrationBuilder.DropTable(
                name: "IpFilters");

            migrationBuilder.DropTable(
                name: "MediaLifecycleRecords");

            migrationBuilder.DropTable(
                name: "MediaRecords");

            migrationBuilder.DropTable(
                name: "ModelCostMappings");

            migrationBuilder.DropTable(
                name: "ModelDeployments");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ProviderHealthConfigurations");

            migrationBuilder.DropTable(
                name: "ProviderHealthRecords");

            migrationBuilder.DropTable(
                name: "ProviderKeyCredentials");

            migrationBuilder.DropTable(
                name: "RequestLogs");

            migrationBuilder.DropTable(
                name: "VirtualKeySpendHistory");

            migrationBuilder.DropTable(
                name: "FallbackConfigurations");

            migrationBuilder.DropTable(
                name: "ModelCosts");

            migrationBuilder.DropTable(
                name: "ModelProviderMappings");

            migrationBuilder.DropTable(
                name: "VirtualKeys");

            migrationBuilder.DropTable(
                name: "RouterConfigEntity");

            migrationBuilder.DropTable(
                name: "Providers");

            migrationBuilder.DropTable(
                name: "VirtualKeyGroups");
        }
    }
}

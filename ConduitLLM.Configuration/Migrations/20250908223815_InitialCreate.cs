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
                name: "MediaRetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PositiveBalanceRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    ZeroBalanceRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    NegativeBalanceRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    SoftDeleteGracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    RespectRecentAccess = table.Column<bool>(type: "boolean", nullable: false),
                    RecentAccessWindowDays = table.Column<int>(type: "integer", nullable: false),
                    IsProTier = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    MaxStorageSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    MaxFileCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRetentionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelAuthors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelAuthors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CostName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PricingModel = table.Column<int>(type: "integer", nullable: false),
                    PricingConfiguration = table.Column<string>(type: "text", nullable: true),
                    InputCostPerMillionTokens = table.Column<decimal>(type: "numeric(18,10)", nullable: false),
                    OutputCostPerMillionTokens = table.Column<decimal>(type: "numeric(18,10)", nullable: false),
                    EmbeddingCostPerMillionTokens = table.Column<decimal>(type: "numeric(18,10)", nullable: true),
                    ImageCostPerImage = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModelType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    VideoCostPerSecond = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    VideoResolutionMultipliers = table.Column<string>(type: "text", nullable: true),
                    BatchProcessingMultiplier = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    SupportsBatchProcessing = table.Column<bool>(type: "boolean", nullable: false),
                    ImageQualityMultipliers = table.Column<string>(type: "text", nullable: true),
                    ImageResolutionMultipliers = table.Column<string>(type: "text", nullable: true),
                    CachedInputCostPerMillionTokens = table.Column<decimal>(type: "numeric(18,10)", nullable: true),
                    CachedInputWriteCostPerMillionTokens = table.Column<decimal>(type: "numeric(18,10)", nullable: true),
                    CostPerSearchUnit = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CostPerInferenceStep = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    DefaultInferenceSteps = table.Column<int>(type: "integer", nullable: true),
                    ReasoningCostPerMillionTokens = table.Column<decimal>(type: "numeric(18,10)", nullable: true)
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
                name: "ProviderTools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    ToolParameters = table.Column<string>(type: "text", nullable: true),
                    CostPerUnit = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    BillingUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CostDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderTools", x => x.Id);
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
                    MediaRetentionPolicyId = table.Column<int>(type: "integer", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeyGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualKeyGroups_MediaRetentionPolicies_MediaRetentionPolic~",
                        column: x => x.MediaRetentionPolicyId,
                        principalTable: "MediaRetentionPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ModelSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthorId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TokenizerType = table.Column<int>(type: "integer", nullable: false),
                    Parameters = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelSeries_ModelAuthors_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "ModelAuthors",
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
                name: "VirtualKeyGroupTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VirtualKeyGroupId = table.Column<int>(type: "integer", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    ReferenceType = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InitiatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InitiatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualKeyGroupTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualKeyGroupTransactions_VirtualKeyGroups_VirtualKeyGrou~",
                        column: x => x.VirtualKeyGroupId,
                        principalTable: "VirtualKeyGroups",
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
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ModelCardUrl = table.Column<string>(type: "text", nullable: true),
                    ModelSeriesId = table.Column<int>(type: "integer", nullable: false),
                    SupportsVision = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsImageGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVideoGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsEmbeddings = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsChat = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsFunctionCalling = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    TokenizerType = table.Column<int>(type: "integer", nullable: false),
                    MaxInputTokens = table.Column<int>(type: "integer", nullable: true),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Parameters = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Models_ModelSeries_ModelSeriesId",
                        column: x => x.ModelSeriesId,
                        principalTable: "ModelSeries",
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
                name: "BillingAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    VirtualKeyId = table.Column<int>(type: "integer", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UsageJson = table.Column<string>(type: "jsonb", nullable: true),
                    CalculatedCost = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProviderType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    ToolUsageJson = table.Column<string>(type: "jsonb", nullable: true),
                    ToolUsageCost = table.Column<decimal>(type: "numeric(10,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingAuditEvents_VirtualKeys_VirtualKeyId",
                        column: x => x.VirtualKeyId,
                        principalTable: "VirtualKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateTable(
                name: "ModelIdentifiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxInputTokens = table.Column<int>(type: "integer", nullable: true),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: true),
                    ProviderVariation = table.Column<string>(type: "text", nullable: true),
                    QualityScore = table.Column<decimal>(type: "numeric", nullable: true),
                    SpeedScore = table.Column<decimal>(type: "numeric", nullable: true),
                    Identifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: true),
                    ModelCostId = table.Column<int>(type: "integer", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelIdentifiers_ModelCosts_ModelCostId",
                        column: x => x.ModelCostId,
                        principalTable: "ModelCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModelIdentifiers_Models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Models",
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModelProviderTypeAssociationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelProviderMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelProviderMappings_ModelIdentifiers_ModelProviderTypeAss~",
                        column: x => x.ModelProviderTypeAssociationId,
                        principalTable: "ModelIdentifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelProviderMappings_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_BillingAuditEvents_EventType",
                table: "BillingAuditEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_EventType_Timestamp",
                table: "BillingAuditEvents",
                columns: new[] { "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_RequestId",
                table: "BillingAuditEvents",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_Timestamp",
                table: "BillingAuditEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_VirtualKeyId",
                table: "BillingAuditEvents",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditEvents_VirtualKeyId_Timestamp",
                table: "BillingAuditEvents",
                columns: new[] { "VirtualKeyId", "Timestamp" });

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
                name: "IX_MediaRetentionPolicies_IsActive",
                table: "MediaRetentionPolicies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRetentionPolicies_IsDefault",
                table: "MediaRetentionPolicies",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRetentionPolicies_Name",
                table: "MediaRetentionPolicies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelAuthor_Name_Unique",
                table: "ModelAuthors",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelCosts_CostName",
                table: "ModelCosts",
                column: "CostName");

            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifier_Identifier",
                table: "ModelIdentifiers",
                column: "Identifier");

            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifier_IsPrimary",
                table: "ModelIdentifiers",
                column: "IsPrimary",
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifier_ModelId",
                table: "ModelIdentifiers",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifier_Provider_Identifier_Unique",
                table: "ModelIdentifiers",
                columns: new[] { "Provider", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelIdentifiers_ModelCostId",
                table: "ModelIdentifiers",
                column: "ModelCostId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMapping_ModelProviderTypeAssociationId",
                table: "ModelProviderMappings",
                column: "ModelProviderTypeAssociationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMapping_ProviderId_IsEnabled",
                table: "ModelProviderMappings",
                columns: new[] { "ProviderId", "IsEnabled" },
                filter: "\"IsEnabled\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviderMappings_ModelAlias_ProviderId",
                table: "ModelProviderMappings",
                columns: new[] { "ModelAlias", "ProviderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Model_ModelSeriesId",
                table: "Models",
                column: "ModelSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelSeries_AuthorId",
                table: "ModelSeries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelSeries_AuthorId_Name_Unique",
                table: "ModelSeries",
                columns: new[] { "AuthorId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelSeries_TokenizerType",
                table: "ModelSeries",
                column: "TokenizerType");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_VirtualKeyId",
                table: "Notifications",
                column: "VirtualKeyId");

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
                name: "IX_ProviderTool_IsActive",
                table: "ProviderTools",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderTool_Provider_ToolName",
                table: "ProviderTools",
                columns: new[] { "Provider", "ToolName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_VirtualKeyId",
                table: "RequestLogs",
                column: "VirtualKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroups_ExternalGroupId",
                table: "VirtualKeyGroups",
                column: "ExternalGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroups_MediaRetentionPolicyId",
                table: "VirtualKeyGroups",
                column: "MediaRetentionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_CreatedAt",
                table: "VirtualKeyGroupTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_IsDeleted_CreatedAt",
                table: "VirtualKeyGroupTransactions",
                columns: new[] { "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_ReferenceType",
                table: "VirtualKeyGroupTransactions",
                column: "ReferenceType");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_TransactionType",
                table: "VirtualKeyGroupTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_VirtualKeyGroupId",
                table: "VirtualKeyGroupTransactions",
                column: "VirtualKeyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualKeyGroupTransactions_VirtualKeyGroupId_CreatedAt",
                table: "VirtualKeyGroupTransactions",
                columns: new[] { "VirtualKeyGroupId", "CreatedAt" });

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
                name: "BatchOperationHistory");

            migrationBuilder.DropTable(
                name: "BillingAuditEvents");

            migrationBuilder.DropTable(
                name: "CacheConfigurationAudits");

            migrationBuilder.DropTable(
                name: "CacheConfigurations");

            migrationBuilder.DropTable(
                name: "GlobalSettings");

            migrationBuilder.DropTable(
                name: "IpFilters");

            migrationBuilder.DropTable(
                name: "MediaRecords");

            migrationBuilder.DropTable(
                name: "ModelProviderMappings");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ProviderKeyCredentials");

            migrationBuilder.DropTable(
                name: "ProviderTools");

            migrationBuilder.DropTable(
                name: "RequestLogs");

            migrationBuilder.DropTable(
                name: "VirtualKeyGroupTransactions");

            migrationBuilder.DropTable(
                name: "VirtualKeySpendHistory");

            migrationBuilder.DropTable(
                name: "ModelIdentifiers");

            migrationBuilder.DropTable(
                name: "Providers");

            migrationBuilder.DropTable(
                name: "VirtualKeys");

            migrationBuilder.DropTable(
                name: "ModelCosts");

            migrationBuilder.DropTable(
                name: "Models");

            migrationBuilder.DropTable(
                name: "VirtualKeyGroups");

            migrationBuilder.DropTable(
                name: "ModelSeries");

            migrationBuilder.DropTable(
                name: "MediaRetentionPolicies");

            migrationBuilder.DropTable(
                name: "ModelAuthors");
        }
    }
}

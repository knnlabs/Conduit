using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Npgsql;
using NpgsqlTypes;

using StackExchange.Redis;

if (args is ["--seed"])
{
    await NativeParityFixture.SeedAsync(ProbeSettings.DatabaseConnectionStringFromEnvironment());
    return;
}

var settings = ProbeSettings.FromEnvironment();
var probe = new GatewayNativeParityProbe(settings);
await probe.RunAsync();

internal sealed record ProbeSettings(
    Uri Admin,
    Uri Gateway,
    Uri SecondaryGateway,
    string RedisConnectionString)
{
    public static ProbeSettings FromEnvironment() => new(
        RequiredUri("CONDUIT_NATIVE_ADMIN_URL"),
        RequiredUri("CONDUIT_NATIVE_GATEWAY_URL"),
        RequiredUri("CONDUIT_NATIVE_GATEWAY_SECONDARY_URL"),
        Required("REDIS_URL").Replace("redis://", string.Empty, StringComparison.OrdinalIgnoreCase));

    public static string DatabaseConnectionStringFromEnvironment()
    {
        var value = Required("DATABASE_URL");
        if (!value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
        }.ConnectionString;
    }

    private static Uri RequiredUri(string name) =>
        new($"{Required(name).TrimEnd('/')}/", UriKind.Absolute);

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required.");
}

internal sealed class GatewayNativeParityProbe(ProbeSettings settings)
{
    private static readonly string[] HubPaths =
    [
        "/hubs/video-generation",
        "/hubs/public/video-generation",
        "/hubs/image-generation",
        "/hubs/tasks",
        "/hubs/notifications",
        "/hubs/spend",
        "/hubs/webhooks",
        "/hubs/virtual-key-management"
    ];

    private readonly HttpClient _admin = CreateClient(settings.Admin);
    private readonly HttpClient _gateway = CreateClient(settings.Gateway);
    private readonly HttpClient _secondaryGateway = CreateClient(settings.SecondaryGateway);

    public async Task RunAsync()
    {
        await AssertRuntimeAndOperationsAsync();
        await AssertSignalRAndRedisAsync();
        Console.WriteLine("Gateway NativeAOT supported protocol/infrastructure probe: PASS");
    }

    private async Task AssertRuntimeAndOperationsAsync()
    {
        using var capabilitiesResponse = await _gateway.GetAsync("health/runtime-capabilities");
        await ExpectAsync(capabilitiesResponse, HttpStatusCode.OK, "runtime capabilities");
        using var capabilities = JsonDocument.Parse(await capabilitiesResponse.Content.ReadAsStringAsync());

        Equal("native-aot", GetString(capabilities.RootElement, "runtime_mode"), "native runtime mode");
        var protocols = GetProperty(capabilities.RootElement, "signal_r_protocols")
            .EnumerateArray().Select(value => value.GetString()).ToArray();
        True(protocols.SequenceEqual(["json"]), "native SignalR protocol is JSON-only");

        var supported = GetProperty(capabilities.RootElement, "included_features")
            .EnumerateArray().Select(value => value.GetString()).ToHashSet();
        True(supported.Contains("authenticated-model-discovery"),
            "authenticated model discovery is advertised");

        var exclusions = GetProperty(capabilities.RootElement, "excluded_features")
            .EnumerateArray().Select(value => value.GetString()).ToHashSet();
        True(exclusions.Contains("signalr-messagepack"), "MessagePack exclusion is observable");
        True(exclusions.Contains("ef-core-query-data-plane"), "EF query data-plane exclusion is observable");
        True(exclusions.Contains("authenticated-signalr-connections"),
            "authenticated SignalR exclusion is observable");
        True(!exclusions.Contains("authenticated-http-data-plane"),
            "authenticated HTTP is no longer excluded as one undifferentiated boundary");

        await ExpectAsync(await _admin.GetAsync("health/live"), HttpStatusCode.OK, "Admin liveness");
        await ExpectAsync(await _gateway.GetAsync("health/live"), HttpStatusCode.OK, "Gateway liveness");
        await ExpectAsync(await _secondaryGateway.GetAsync("health/runtime-capabilities"),
            HttpStatusCode.OK, "secondary native Gateway");

        using var metrics = await _gateway.GetAsync("metrics");
        await ExpectAsync(metrics, HttpStatusCode.OK, "Prometheus metrics");
        True((await metrics.Content.ReadAsStringAsync()).Contains("# HELP", StringComparison.Ordinal),
            "Prometheus exposition body");

        await ExpectAsync(await _gateway.GetAsync("v1/models"), HttpStatusCode.Unauthorized,
            "unauthenticated data-plane request is rejected before EF");

        foreach (var gateway in new[] { _gateway, _secondaryGateway })
        {
            using var listRequest = AuthorizedRequest(HttpMethod.Get, "v1/models");
            using var listResponse = await gateway.SendAsync(listRequest);
            await ExpectAsync(listResponse, HttpStatusCode.OK, "typed-store authenticated model list");
            var listBody = await listResponse.Content.ReadAsStringAsync();
            True(listBody.Contains(NativeParityFixture.ModelAlias, StringComparison.Ordinal),
                "model list contains seeded native alias");

            using var retrieveRequest = AuthorizedRequest(
                HttpMethod.Get,
                $"v1/models/{NativeParityFixture.ModelAlias}");
            using var retrieveResponse = await gateway.SendAsync(retrieveRequest);
            await ExpectAsync(retrieveResponse, HttpStatusCode.OK, "typed-store model retrieval");
        }

        using var metadataRequest = AuthorizedRequest(
            HttpMethod.Get,
            $"v1/conduit/models/{NativeParityFixture.ModelAlias}/metadata");
        using var metadataResponse = await _gateway.SendAsync(metadataRequest);
        await ExpectAsync(metadataResponse, HttpStatusCode.OK, "typed-store model capability metadata");
        using var metadataDocument = JsonDocument.Parse(
            await metadataResponse.Content.ReadAsStringAsync());
        var metadata = GetProperty(metadataDocument.RootElement, "metadata");
        var capabilitiesGraph = GetProperty(metadata, "capabilities");
        True(GetProperty(capabilitiesGraph, "chat").GetBoolean(),
            "model metadata contains effective chat capability");
    }

    private async Task AssertSignalRAndRedisAsync()
    {
        foreach (var gateway in new[] { _gateway, _secondaryGateway })
        {
            foreach (var hubPath in HubPaths)
            {
                using var response = await gateway.PostAsync($"{hubPath}/negotiate?negotiateVersion=1", null);
                True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized,
                    $"JSON SignalR negotiate route {hubPath} is active");

                using var authenticatedRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{hubPath}/negotiate?negotiateVersion=1");
                authenticatedRequest.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    NativeParityFixture.VirtualKey);
                using var authenticatedResponse = await gateway.SendAsync(authenticatedRequest);
                await ExpectAsync(
                    authenticatedResponse,
                    HttpStatusCode.OK,
                    $"typed-store authentication and IP policy for {hubPath}");
            }
        }

        await using var redis = await ConnectionMultiplexer.ConnectAsync(settings.RedisConnectionString);
        var endpoint = redis.GetEndPoints().Single();
        var server = redis.GetServer(endpoint);
        var channels = server.SubscriptionChannels(
            new RedisChannel("conduit_signalr:*", RedisChannel.PatternMode.Pattern));
        True(channels.Length > 0, "Redis SignalR backplane subscriptions");
        True(await redis.GetDatabase().KeyExistsAsync("Conduit-DataProtection-Keys"),
            "Redis data-protection key ring");
    }

    private static HttpClient CreateClient(Uri baseAddress) => new()
    {
        BaseAddress = baseAddress,
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            NativeParityFixture.VirtualKey);
        return request;
    }

    private static async Task ExpectAsync(HttpResponseMessage response, HttpStatusCode expected, string name)
    {
        if (response.StatusCode != expected)
        {
            throw new InvalidOperationException(
                $"{name}: expected {(int)expected}, received {(int)response.StatusCode}: " +
                await response.Content.ReadAsStringAsync());
        }
        Console.WriteLine($"  PASS  {name}");
    }

    private static JsonElement GetProperty(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }
        throw new InvalidOperationException($"Response did not contain '{name}': {element}");
    }

    private static string GetString(JsonElement element, string name) =>
        GetProperty(element, name).GetString()
        ?? throw new InvalidOperationException($"'{name}' was null.");

    private static void True(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {name}");
        }
        Console.WriteLine($"  PASS  {name}");
    }

    private static void Equal(string expected, string actual, string name) =>
        True(string.Equals(expected, actual, StringComparison.Ordinal), $"{name} ({actual})");
}

internal static class NativeParityFixture
{
    public const string VirtualKey = "condt_native_aot_parity";
    public const string ModelAlias = "native-aot-model";
    private const string FixtureOwner = "native-aot-parity";

    public static async Task SeedAsync(string connectionString)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var now = DateTime.UtcNow;

        await UpsertSettingAsync(connection, transaction, "IpFilter:Enabled", "true", now);
        await UpsertSettingAsync(connection, transaction, "IpFilter:DefaultAllow", "false", now);
        var groupId = await UpsertGroupAsync(connection, transaction, now);
        var keyId = await UpsertVirtualKeyAsync(connection, transaction, groupId, now);
        await ReplaceIpFiltersAsync(connection, transaction, keyId, now);
        await UpsertModelRoutingAsync(connection, transaction, now);

        await transaction.CommitAsync();
        Console.WriteLine("Seeded native Gateway virtual-key, IP-filter, and model-routing parity fixture.");
    }

    private static async Task UpsertModelRoutingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTime now)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO "Providers" (
                "Id", "ProviderType", "ProviderName", "BaseUrl", "Settings", "IsEnabled",
                "TrustProviderReportedCosts", "ProviderCostMarkupMultiplier", "CreatedAt", "UpdatedAt")
            VALUES (-9001, 1, 'Native AOT parity provider', NULL, '{"region":"native"}'::jsonb,
                true, true, 1.125, @now, @now)
            ON CONFLICT ("Id") DO UPDATE SET
                "ProviderType" = EXCLUDED."ProviderType",
                "ProviderName" = EXCLUDED."ProviderName",
                "Settings" = EXCLUDED."Settings",
                "IsEnabled" = true,
                "UpdatedAt" = EXCLUDED."UpdatedAt";

            INSERT INTO "ModelAuthors" ("Id", "Name", "Description", "WebsiteUrl")
            VALUES (-9002, 'Native AOT parity author', 'Native process fixture', NULL)
            ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name";

            INSERT INTO "ModelSeries" (
                "Id", "AuthorId", "Name", "Description", "TokenizerType", "Parameters")
            VALUES (-9003, -9002, 'Native AOT parity series', NULL, 0, '{"temperature":{}}')
            ON CONFLICT ("Id") DO UPDATE SET
                "AuthorId" = EXCLUDED."AuthorId",
                "Name" = EXCLUDED."Name",
                "Parameters" = EXCLUDED."Parameters";

            INSERT INTO "Models" (
                "Id", "Name", "Version", "Description", "ModelCardUrl", "ModelSeriesId",
                "SupportsVision", "SupportsImageGeneration", "SupportsVideoGeneration",
                "SupportsEmbeddings", "SupportsSpeechToText", "SupportsTextToSpeech",
                "SupportsRerank", "SupportsChat", "SupportsFunctionCalling", "SupportsStreaming",
                "InputModalities", "OutputModalities", "CapabilitySource",
                "CapabilitiesLastVerifiedAt", "TokenizerType", "MaxInputTokens", "MaxOutputTokens",
                "IsActive", "Parameters", "CreatedAt", "UpdatedAt")
            VALUES (-9004, 'Native AOT parity model', 'v1', 'Native process routing fixture', NULL, -9003,
                true, false, false, false, false, false, false, true, true, true,
                '["text","image"]'::jsonb, '["text"]'::jsonb, 2, @now, 0, 64000, 8192,
                true, NULL, @now, @now)
            ON CONFLICT ("Id") DO UPDATE SET
                "Name" = EXCLUDED."Name",
                "ModelSeriesId" = EXCLUDED."ModelSeriesId",
                "SupportsVision" = true,
                "SupportsChat" = true,
                "SupportsFunctionCalling" = true,
                "SupportsStreaming" = true,
                "InputModalities" = EXCLUDED."InputModalities",
                "OutputModalities" = EXCLUDED."OutputModalities",
                "UpdatedAt" = EXCLUDED."UpdatedAt";

            INSERT INTO "ModelCosts" (
                "Id", "CostName", "PricingModel", "PricingConfiguration",
                "InputCostPerMillionTokens", "OutputCostPerMillionTokens",
                "EmbeddingCostPerMillionTokens", "CreatedAt", "UpdatedAt", "ModelType",
                "IsActive", "EffectiveDate", "ExpiryDate", "Description", "Priority",
                "BatchProcessingMultiplier", "SupportsBatchProcessing",
                "CachedInputCostPerMillionTokens", "CachedInputWriteCostPerMillionTokens",
                "CostPerSearchUnit", "AudioCostPerMinute", "AudioCostPerThousandCharacters",
                "ReasoningCostPerMillionTokens")
            VALUES (-9005, 'Native AOT parity cost', 0, NULL, 2.5, 10, NULL,
                @now, @now, 'chat', true, @now, NULL, NULL, 0, 0.5, true,
                0.25, NULL, NULL, NULL, NULL, NULL)
            ON CONFLICT ("Id") DO UPDATE SET
                "InputCostPerMillionTokens" = EXCLUDED."InputCostPerMillionTokens",
                "OutputCostPerMillionTokens" = EXCLUDED."OutputCostPerMillionTokens",
                "UpdatedAt" = EXCLUDED."UpdatedAt";

            INSERT INTO "ModelIdentifiers" (
                "Id", "ModelId", "IsEnabled", "MaxInputTokens", "MaxOutputTokens",
                "InputModalities", "OutputModalities", "OperationalCapabilities",
                "CapabilitySource", "CapabilitiesLastVerifiedAt", "ProviderVariation",
                "QualityScore", "SpeedScore", "Identifier", "Provider", "ModelCostId",
                "IsPrimary", "Metadata")
            VALUES (-9006, -9004, true, 32000, 4096, '["text","image"]'::jsonb,
                '["text"]'::jsonb, '{"supports_json_schema":true}'::jsonb, 2, @now,
                NULL, 0.95, 1.5, 'native-aot-provider-model', 1, -9005, true, NULL)
            ON CONFLICT ("Id") DO UPDATE SET
                "ModelId" = EXCLUDED."ModelId",
                "IsEnabled" = true,
                "OperationalCapabilities" = EXCLUDED."OperationalCapabilities",
                "ModelCostId" = EXCLUDED."ModelCostId";

            INSERT INTO "ModelProviderMappings" (
                "Id", "ModelAlias", "ProviderModelId", "ProviderId", "IsEnabled",
                "RoutingPriority", "RoutingWeight", "ProviderOptions", "CreatedAt", "UpdatedAt",
                "ModelProviderTypeAssociationId")
            VALUES (-9007, @alias, 'native-aot-provider-model', -9001, true,
                10, 1.25, '{"route":"fallback"}', @now, @now, -9006)
            ON CONFLICT ("Id") DO UPDATE SET
                "ModelAlias" = EXCLUDED."ModelAlias",
                "ProviderId" = EXCLUDED."ProviderId",
                "IsEnabled" = true,
                "ModelProviderTypeAssociationId" = EXCLUDED."ModelProviderTypeAssociationId",
                "UpdatedAt" = EXCLUDED."UpdatedAt";

            INSERT INTO "ModelRoutePolicies" (
                "Id", "ModelAlias", "Strategy", "CostWeight", "SpeedWeight", "QualityWeight",
                "CacheAffinityEnabled", "AffinityTtlSeconds", "MaxAffinityScorePenalty",
                "IsEnabled", "CreatedAt", "UpdatedAt")
            VALUES (-9008, @alias, 'Balanced', 0.5, 0.3, 0.2, true, 900, 0.075,
                true, @now, @now)
            ON CONFLICT ("Id") DO UPDATE SET
                "ModelAlias" = EXCLUDED."ModelAlias",
                "IsEnabled" = true,
                "UpdatedAt" = EXCLUDED."UpdatedAt";
            """;
        command.Parameters.AddWithValue("alias", NpgsqlDbType.Varchar, ModelAlias);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpsertSettingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string key,
        string value,
        DateTime now)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO "GlobalSettings" ("Key", "Value", "Description", "CreatedAt", "UpdatedAt")
            VALUES (@key, @value, @description, @now, @now)
            ON CONFLICT ("Key") DO UPDATE
            SET "Value" = EXCLUDED."Value",
                "Description" = EXCLUDED."Description",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;
        command.Parameters.AddWithValue("key", NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue("value", NpgsqlDbType.Text, value);
        command.Parameters.AddWithValue(
            "description",
            NpgsqlDbType.Text,
            "Native Gateway parity fixture");
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> UpsertGroupAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTime now)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH updated AS (
                UPDATE "VirtualKeyGroups"
                SET "GroupName" = @name,
                    "Balance" = 100.0,
                    "LifetimeCreditsAdded" = 100.0,
                    "LifetimeSpent" = 0.0,
                    "UpdatedAt" = @now
                WHERE "ExternalGroupId" = @externalId
                RETURNING "Id"
            ), inserted AS (
                INSERT INTO "VirtualKeyGroups" (
                    "ExternalGroupId", "GroupName", "Balance", "LifetimeCreditsAdded",
                    "LifetimeSpent", "CreatedAt", "UpdatedAt")
                SELECT @externalId, @name, 100.0, 100.0, 0.0, @now, @now
                WHERE NOT EXISTS (SELECT 1 FROM updated)
                RETURNING "Id"
            )
            SELECT "Id" FROM updated
            UNION ALL
            SELECT "Id" FROM inserted
            LIMIT 1
            """;
        command.Parameters.AddWithValue("externalId", NpgsqlDbType.Varchar, FixtureOwner);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, "Native AOT parity group");
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Native parity group upsert returned no ID."));
    }

    private static async Task<int> UpsertVirtualKeyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int groupId,
        DateTime now)
    {
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(VirtualKey)))
            .ToLowerInvariant();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO "VirtualKeys" (
                "KeyName", "KeyHash", "Description", "IsEnabled", "VirtualKeyGroupId",
                "CreatedAt", "UpdatedAt")
            VALUES (@name, @hash, @description, true, @groupId, @now, @now)
            ON CONFLICT ("KeyHash") DO UPDATE
            SET "KeyName" = EXCLUDED."KeyName",
                "Description" = EXCLUDED."Description",
                "IsEnabled" = true,
                "VirtualKeyGroupId" = EXCLUDED."VirtualKeyGroupId",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            RETURNING "Id"
            """;
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, "Native AOT parity key");
        command.Parameters.AddWithValue("hash", NpgsqlDbType.Varchar, keyHash);
        command.Parameters.AddWithValue("description", NpgsqlDbType.Varchar, "Native Gateway parity fixture");
        command.Parameters.AddWithValue("groupId", NpgsqlDbType.Integer, groupId);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Native parity key upsert returned no ID."));
    }

    private static async Task ReplaceIpFiltersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int keyId,
        DateTime now)
    {
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM \"IpFilters\" WHERE \"CreatedBy\" = @owner";
            delete.Parameters.AddWithValue("owner", NpgsqlDbType.Varchar, FixtureOwner);
            await delete.ExecuteNonQueryAsync();
        }

        await InsertIpFilterAsync(connection, transaction, virtualKeyId: null, "global", now);
        await InsertIpFilterAsync(connection, transaction, keyId, "virtual key", now);
    }

    private static async Task InsertIpFilterAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int? virtualKeyId,
        string scope,
        DateTime now)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO "IpFilters" (
                "FilterType", "IpAddressOrCidr", "Name", "Description", "IsEnabled",
                "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "VirtualKeyId")
            VALUES (
                'whitelist', '127.0.0.1', @name, @description, true,
                @now, @now, @owner, @owner, @virtualKeyId)
            """;
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, $"Native parity {scope} allowlist");
        command.Parameters.AddWithValue("description", NpgsqlDbType.Varchar, "Allows the local native parity client");
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("owner", NpgsqlDbType.Varchar, FixtureOwner);
        command.Parameters.Add(new NpgsqlParameter("virtualKeyId", NpgsqlDbType.Integer)
        {
            Value = virtualKeyId.HasValue ? virtualKeyId.Value : DBNull.Value
        });
        await command.ExecuteNonQueryAsync();
    }
}

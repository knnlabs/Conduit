using System.Net.Http;
using System.Text.Json;

using AdminOperationIdValidator = ConduitLLM.Admin.OpenApi.OperationIdValidationDocumentTransformer;

using FluentAssertions;

using Microsoft.OpenApi;

namespace ConduitLLM.Tests.OpenApi;

[Trait("Category", "Unit")]
[Trait("Component", "OpenApi")]
public sealed class AuthoritativeContractTests : IDisposable
{
    private readonly JsonDocument _admin = LoadContract("Services", "ConduitLLM.Admin", "openapi-admin.json");
    private readonly JsonDocument _gateway = LoadContract("Services", "ConduitLLM.Gateway", "openapi-gateway.json");

    [Fact]
    public void Contracts_PublishStableGeneratedAndExplicitOperationIds()
    {
        Operation(_admin, "/api/VirtualKeys", "get").GetProperty("operationId").GetString()
            .Should().Be("VirtualKeys_GetAll");
        Operation(_admin, "/api/ModelAuthor", "get").GetProperty("operationId").GetString()
            .Should().Be("ModelAuthors_List");
        Operation(_admin, "/api/ModelAuthor/{id}", "get").GetProperty("operationId").GetString()
            .Should().Be("ModelAuthors_GetById", "explicit operation IDs must follow Tag_Action");
    }

    [Fact]
    public void DuplicateOperationIds_AreRejected()
    {
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/one"] = PathWith(HttpMethod.Get, "Duplicate_Id"),
                ["/two"] = PathWith(HttpMethod.Post, "Duplicate_Id")
            }
        };

        var act = () => AdminOperationIdValidator.Validate(document);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate_Id*");
    }

    [Fact]
    public void Contracts_DefineCanonicalSecuritySchemes()
    {
        var adminScheme = Scheme(_admin, "MasterKey");
        adminScheme.GetProperty("type").GetString().Should().Be("apiKey");
        adminScheme.GetProperty("name").GetString().Should().Be("X-Master-Key");
        adminScheme.GetProperty("in").GetString().Should().Be("header");

        var gatewayScheme = Scheme(_gateway, "VirtualKey");
        gatewayScheme.GetProperty("type").GetString().Should().Be("http");
        gatewayScheme.GetProperty("scheme").GetString().Should().Be("bearer");
        gatewayScheme.GetProperty("bearerFormat").GetString().Should().Be("opaque virtual key");
    }

    [Theory]
    [InlineData("admin", "/api/VirtualKeys/validate", "post")]
    [InlineData("admin", "/api/IpFilter/check/{ipAddress}", "get")]
    [InlineData("gateway", "/v1/media/{storageKey}", "get")]
    [InlineData("gateway", "/v1/media/{storageKey}", "head")]
    public void AnonymousOperations_PublishAnExplicitEmptySecurityRequirement(
        string contract,
        string path,
        string method)
    {
        var document = contract == "admin" ? _admin : _gateway;

        Operation(document, path, method).GetProperty("security").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("admin", "/api/VirtualKeys", "get", "MasterKey")]
    [InlineData("gateway", "/v1/chat/completions", "post", "VirtualKey")]
    public void AuthenticatedOperations_ReferenceTheirCanonicalScheme(
        string contract,
        string path,
        string method,
        string scheme)
    {
        var document = contract == "admin" ? _admin : _gateway;
        var requirement = Operation(document, path, method).GetProperty("security")[0];

        requirement.TryGetProperty(scheme, out _).Should().BeTrue();
    }

    [Fact]
    public void Gateway_PublishesBinaryAndStreamingResponses()
    {
        var mediaSchema = Operation(_gateway, "/v1/media/{storageKey}", "get")
            .GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/octet-stream").GetProperty("schema");
        mediaSchema.GetProperty("type").GetString().Should().Be("string");
        mediaSchema.GetProperty("format").GetString().Should().Be("binary");

        var downloadSchema = Operation(_gateway, "/v1/downloads/{fileId}", "get")
            .GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/octet-stream").GetProperty("schema");
        downloadSchema.GetProperty("type").GetString().Should().Be("string");
        downloadSchema.GetProperty("format").GetString().Should().Be("binary");

        var streamSchema = Operation(_gateway, "/v1/chat/completions", "post")
            .GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("text/event-stream").GetProperty("schema");
        streamSchema.GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void Admin_Universal500UsesTheStandardErrorShape()
    {
        var properties = Operation(_admin, "/api/VirtualKeys", "get")
            .GetProperty("responses").GetProperty("500").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("properties");

        properties.TryGetProperty("error", out _).Should().BeTrue();
        properties.TryGetProperty("details", out _).Should().BeTrue();
        properties.TryGetProperty("code", out _).Should().BeTrue();
    }

    [Fact]
    public void Admin_ModelReadsPublishTypedResponseContracts()
    {
        var flatOperation = Operation(_admin, "/api/Model", "get");
        var flatSchema = flatOperation.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        flatSchema.GetProperty("type").GetString().Should().Be("array");
        flatSchema.GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/ModelDto");
        flatOperation.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .Should().NotContain(["page", "pageSize"]);

        ResponseSchema(_admin, "/api/Model/paged")
            .GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/PagedResultOfModelDto");
        ResponseSchema(_admin, "/api/Model/{id}/identifiers")
            .GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/ModelIdentifierDto");
        ResponseSchema(_admin, "/api/Model/{id}/available-providers")
            .GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/ModelProviderAvailabilityDto");
    }

    [Fact]
    public void Admin_ModelIdentifierCreationPublishesTypedResponseContract()
    {
        Operation(_admin, "/api/Model/{id}/identifiers", "post")
            .GetProperty("responses").GetProperty("201")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/CreatedModelIdentifierDto");
    }

    [Theory]
    [InlineData("/api/ModelAuthor", "post", "CreateModelAuthorDto", "201", "ModelAuthorDto")]
    [InlineData("/api/ModelAuthor/{id}", "put", "UpdateModelAuthorDto", "204", null)]
    [InlineData("/api/ModelAuthor/{id}", "delete", null, "204", null)]
    [InlineData("/api/ModelSeries", "post", "CreateModelSeriesDto", "201", "ModelSeriesDto")]
    [InlineData("/api/ModelSeries/{id}", "put", "UpdateModelSeriesDto", "204", null)]
    [InlineData("/api/ModelSeries/{id}", "delete", null, "204", null)]
    [InlineData("/api/Model", "post", "CreateModelDto", "201", "ModelDto")]
    [InlineData("/api/Model/{id}", "put", "UpdateModelDto", "200", "ModelDto")]
    [InlineData("/api/Model/{id}", "delete", null, "204", null)]
    [InlineData("/api/Model/{id}/identifiers", "post", "CreateModelIdentifierDto", "201", "CreatedModelIdentifierDto")]
    [InlineData("/api/Model/{id}/identifiers/{identifierId}", "put", "UpdateModelIdentifierDto", "204", null)]
    [InlineData("/api/Model/{id}/identifiers/{identifierId}", "delete", null, "204", null)]
    [InlineData("/api/Model/{id}/provider-mappings", "post", "ModelProviderMappingDto", "201", "ModelProviderMappingDto")]
    [InlineData("/api/Model/{id}/provider-mappings/{mappingId}", "put", "ModelProviderMappingDto", "204", null)]
    [InlineData("/api/Model/{id}/provider-mappings/{mappingId}", "delete", null, "204", null)]
    [InlineData("/api/Model/bundled-catalog/import", "post", null, "200", "BundledModelCatalogImportResult")]
    public void Admin_ModelFamilyMutationsPublishConcreteContracts(
        string path,
        string method,
        string? requestSchema,
        string responseStatus,
        string? responseSchema)
    {
        var operation = Operation(_admin, path, method);

        if (requestSchema is null)
        {
            operation.TryGetProperty("requestBody", out _).Should().BeFalse();
        }
        else
        {
            operation.GetProperty("requestBody").GetProperty("content")
                .GetProperty("application/json").GetProperty("schema")
                .GetProperty("$ref").GetString()
                .Should().Be($"#/components/schemas/{requestSchema}");
        }

        var response = operation.GetProperty("responses").GetProperty(responseStatus);
        if (responseSchema is null)
        {
            response.TryGetProperty("content", out _).Should().BeFalse();
        }
        else
        {
            response.GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString()
                .Should().Be($"#/components/schemas/{responseSchema}");
        }
    }

    [Fact]
    public void Admin_TopLevelModelMappingsPublishConcreteContracts()
    {
        ResponseSchema(_admin, "/api/ModelProviderMapping")
            .GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/ModelProviderMappingDto");
        ResponseSchema(_admin, "/api/ModelProviderMapping/{id}")
            .GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/ModelProviderMappingDto");

        RequestSchema(_admin, "/api/ModelProviderMapping", "post")
            .GetProperty("$ref").GetString().Should().Be("#/components/schemas/CreateModelProviderMappingDto");
        RequestSchema(_admin, "/api/ModelProviderMapping/{id}", "put")
            .GetProperty("$ref").GetString().Should().Be("#/components/schemas/UpdateModelProviderMappingDto");
        foreach (var schemaName in new[]
        {
            "CreateModelProviderMappingDto",
            "UpdateModelProviderMappingDto",
            "ModelProviderMappingDto"
        })
        {
            _admin.RootElement.GetProperty("components").GetProperty("schemas")
                .GetProperty(schemaName).GetProperty("properties")
                .TryGetProperty("notes", out _).Should().BeFalse();
        }
        RequestSchema(_admin, "/api/ModelProviderMapping/bulk", "post")
            .GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/BulkModelMappingCreateRequest");
        RequestSchema(_admin, "/api/ModelProviderMapping/bulk/preview", "post")
            .GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/BulkModelMappingPreviewRequest");

        foreach (var (path, responseSchema) in new[]
        {
            ("/api/ModelProviderMapping/bulk/preview", "BulkModelMappingPreviewResponse"),
            ("/api/ModelProviderMapping/bulk", "BulkModelMappingCreateResponse"),
            ("/api/ModelProviderMapping/bulk/delete", "BulkDeleteResult"),
            ("/api/ModelProviderMapping/bulk/enable", "BulkUpdateResult"),
            ("/api/ModelProviderMapping/bulk/disable", "BulkUpdateResult")
        })
        {
            Operation(_admin, path, "post").GetProperty("responses").GetProperty("200")
                .GetProperty("content").GetProperty("application/json").GetProperty("schema")
                .GetProperty("$ref").GetString().Should().Be($"#/components/schemas/{responseSchema}");
        }

        foreach (var method in new[] { "put", "delete" })
        {
            Operation(_admin, "/api/ModelProviderMapping/{id}", method)
                .GetProperty("responses").GetProperty("204")
                .TryGetProperty("content", out _).Should().BeFalse();
        }
        Operation(_admin, "/api/ModelProviderMapping/{id}", "put")
            .GetProperty("responses").TryGetProperty("409", out _).Should().BeTrue();

        Operation(_admin, "/api/ModelProviderMapping/{id}", "get").GetProperty("parameters")[0]
            .GetProperty("schema").GetProperty("format").GetString().Should().Be("int32");
    }

    [Theory]
    [InlineData("ModelProviderMappingDto", "id", "modelAlias", "providerModelId", "providerId", "modelProviderTypeAssociationId", "priority", "weight", "isEnabled", "createdAt", "updatedAt")]
    [InlineData("ProviderReferenceDto", "id", "providerType", "displayName", "isEnabled")]
    [InlineData("ModelCapabilitiesDto", "supportsVision", "supportsImageGeneration", "supportsVideoGeneration", "supportsEmbeddings", "supportsSpeechToText", "supportsTextToSpeech", "supportsRerank", "supportsChat", "supportsFunctionCalling", "supportsStreaming", "maxInputTokens", "maxOutputTokens")]
    [InlineData("BulkModelMappingPreviewResponse", "items", "totalProcessed", "conflictCount")]
    [InlineData("BulkModelMappingCreateResponse", "created", "existing", "failed", "totalProcessed", "createdCount", "existingCount", "successCount", "failureCount", "isSuccess", "isPartialSuccess")]
    [InlineData("BulkDeleteResult", "deletedIds", "errors", "totalProcessed", "successCount", "failureCount")]
    [InlineData("BulkUpdateResult", "updated", "errors", "totalProcessed", "successCount", "failureCount")]
    public void Admin_ModelMappingResponsesRequireAlwaysEmittedProperties(string schema, params string[] properties)
    {
        var required = _admin.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(schema).GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()).ToList();
        required.Should().Contain(properties);
    }

    [Theory]
    [InlineData("GlobalSettingDto", "id", "key", "value", "description", "createdAt", "updatedAt")]
    [InlineData("GlobalSettingCacheStatsDto", "cacheSize", "cacheHits", "cacheMisses", "invalidations", "hitRate", "lastLoadTime", "cachedKeys")]
    public void Admin_GlobalSettingsResponsesRequireAlwaysEmittedProperties(string schema, params string[] properties)
    {
        var required = _admin.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(schema).GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()).ToList();
        required.Should().Contain(properties);
    }

    [Theory]
    [InlineData("IpFilterDto", "id", "filterType", "ipAddressOrCidr", "name", "isEnabled", "createdAt", "updatedAt")]
    [InlineData("IpFilterSettingsDto", "isEnabled", "defaultAllow", "bypassForAdminUi", "excludedEndpoints", "filterMode", "whitelistFilters", "blacklistFilters")]
    [InlineData("IpCheckResult", "isAllowed")]
    public void Admin_IpFilterResponsesRequireAlwaysEmittedProperties(string schema, params string[] properties)
    {
        var required = _admin.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(schema).GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()).ToList();
        required.Should().Contain(properties);
    }

    [Theory]
    [InlineData("/api/FunctionConfigurations", "FunctionConfigurationDto")]
    [InlineData("/api/FunctionCredentials", "FunctionCredential")]
    [InlineData("/api/FunctionCosts", "FunctionCostDto")]
    [InlineData("/api/FunctionExecutions/expired-leases", "FunctionExecutionDto")]
    public void Admin_FunctionListsPublishTypedItems(string path, string schema)
    {
        ResponseSchema(_admin, path).GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be($"#/components/schemas/{schema}");
    }

    [Theory]
    [InlineData("/api/FunctionConfigurations/{id}", "FunctionConfigurationDto")]
    [InlineData("/api/FunctionCredentials/{id}", "FunctionCredential")]
    [InlineData("/api/FunctionCosts/{id}", "FunctionCostDto")]
    [InlineData("/api/FunctionExecutions/{id}", "FunctionExecutionDto")]
    public void Admin_FunctionEntityReadsPublishTypedResponses(string path, string schema)
    {
        ResponseSchema(_admin, path).GetProperty("$ref").GetString()
            .Should().Be($"#/components/schemas/{schema}");
    }

    [Theory]
    [InlineData("/api/FunctionCredentials/test", "post", "FunctionCredentialTestResultDto")]
    [InlineData("/api/FunctionCosts/cache/clear", "post", "FunctionCostCacheClearResultDto")]
    [InlineData("/api/FunctionExecutions/cleanup", "delete", "FunctionExecutionCleanupResultDto")]
    public void Admin_FunctionAnonymousResultsUseNamedSchemas(string path, string method, string schema)
    {
        Operation(_admin, path, method).GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("$ref").GetString().Should().Be($"#/components/schemas/{schema}");
    }

    [Fact]
    public void Contracts_DoNotPublishBareJsonSuccessSchemas()
    {
        foreach (var (name, document) in new[] { ("admin", _admin), ("gateway", _gateway) })
        {
            foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            {
                foreach (var operation in path.Value.EnumerateObject()
                    .Where(member => new[] { "get", "put", "post", "delete", "options", "head", "patch", "trace" }
                        .Contains(member.Name)))
                {
                    foreach (var response in operation.Value.GetProperty("responses").EnumerateObject()
                        .Where(member => member.Name.StartsWith('2')))
                    {
                        if (!response.Value.TryGetProperty("content", out var content) ||
                            !content.TryGetProperty("application/json", out var json))
                        {
                            continue;
                        }

                        IsGenericSuccessSchema(json.GetProperty("schema")).Should().BeFalse(
                            $"{name} {operation.Name.ToUpperInvariant()} {path.Name} response {response.Name} must publish a concrete JSON schema");
                    }
                }
            }
        }
    }

    [Fact]
    public void Admin_FunctionConfigurationContractsUseBoundaryDtos()
    {
        RequestSchema(_admin, "/api/FunctionConfigurations", "post")
            .GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/CreateFunctionConfigurationRequest");
        RequestSchema(_admin, "/api/FunctionConfigurations/{id}", "put")
            .GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/UpdateFunctionConfigurationRequest");

        var schemas = _admin.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("FunctionConfiguration", out _).Should().BeFalse(
            "the persistence entity must not be part of the public contract");
    }

    [Fact]
    public void Admin_FunctionExecutionContractUsesStructuredJsonAndHidesLeaseInternals()
    {
        var properties = _admin.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("FunctionExecutionDto").GetProperty("properties");

        JsonElementReference(properties.GetProperty("request"))
            .Should().Be("#/components/schemas/JsonElement");
        JsonElementReference(properties.GetProperty("response"))
            .Should().Be("#/components/schemas/JsonElement");
        JsonElementReference(properties.GetProperty("costCalculation"))
            .Should().Be("#/components/schemas/JsonElement");
        properties.TryGetProperty("requestJson", out _).Should().BeFalse();
        properties.TryGetProperty("responseJson", out _).Should().BeFalse();
        properties.TryGetProperty("leasedBy", out _).Should().BeFalse();
        properties.TryGetProperty("leaseExpiryTime", out _).Should().BeFalse();
        properties.TryGetProperty("version", out _).Should().BeFalse();
    }

    public void Dispose()
    {
        _admin.Dispose();
        _gateway.Dispose();
    }

    private static OpenApiPathItem PathWith(HttpMethod method, string operationId) => new()
    {
        Operations = new Dictionary<HttpMethod, OpenApiOperation>
        {
            [method] = new() { OperationId = operationId }
        }
    };

    private static JsonElement Operation(JsonDocument document, string path, string method) =>
        document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static JsonElement Scheme(JsonDocument document, string name) =>
        document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty(name);

    private static JsonElement ResponseSchema(JsonDocument document, string path) =>
        Operation(document, path, "get").GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");

    private static JsonElement RequestSchema(JsonDocument document, string path, string method) =>
        Operation(document, path, method).GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");

    private static bool IsGenericSuccessSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.EnumerateObject().Any())
        {
            return true;
        }
        if (schema.TryGetProperty("$ref", out _))
        {
            return false;
        }
        foreach (var composition in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (schema.TryGetProperty(composition, out var branches))
            {
                return !branches.EnumerateArray().Any() ||
                    branches.EnumerateArray().All(IsGenericSuccessSchema);
            }
        }
        if (!schema.TryGetProperty("type", out var type) || type.GetString() != "object")
        {
            if (type.ValueKind == JsonValueKind.String && type.GetString() == "array")
            {
                return !schema.TryGetProperty("items", out var items) || IsGenericSuccessSchema(items);
            }
            return false;
        }

        return (!schema.TryGetProperty("properties", out var properties) ||
                !properties.EnumerateObject().Any()) &&
            !schema.TryGetProperty("additionalProperties", out _) &&
            !schema.TryGetProperty("patternProperties", out _);
    }

    private static string? JsonElementReference(JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var direct))
        {
            return direct.GetString();
        }

        return schema.GetProperty("oneOf").EnumerateArray()
            .First(branch => branch.TryGetProperty("$ref", out _))
            .GetProperty("$ref").GetString();
    }

    private static JsonDocument LoadContract(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Conduit.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the test must run from a ConduitLLM checkout");
        return JsonDocument.Parse(File.ReadAllText(Path.Combine([directory!.FullName, .. relativeSegments])));
    }
}

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
            .Should().Be("VirtualKeys_ListKeys");
        Operation(_admin, "/api/ModelAuthor", "get").GetProperty("operationId").GetString()
            .Should().Be("ModelAuthors_List");
        Operation(_admin, "/api/ModelAuthor/{id}", "get").GetProperty("operationId").GetString()
            .Should().Be("GetModelAuthorById", "explicit operation IDs must remain stable");
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

    private static JsonDocument LoadContract(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Conduit.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the test must run from a ConduitLLM checkout");
        return JsonDocument.Parse(File.ReadAllText(Path.Combine([directory!.FullName, .. relativeSegments])));
    }
}

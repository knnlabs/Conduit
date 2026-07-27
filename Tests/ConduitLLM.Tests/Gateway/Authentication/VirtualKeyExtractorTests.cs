using ConduitLLM.Gateway.Authentication;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

namespace ConduitLLM.Tests.Gateway.Authentication;

/// <summary>
/// Pins the credential-extraction policy: query-string credentials are accepted only on
/// SignalR hub paths, header credentials everywhere (#1263).
/// </summary>
[Trait("Category", "Unit")]
public class VirtualKeyExtractorTests
{
    private static DefaultHttpContext Context(string path, string? queryString = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (queryString != null)
        {
            context.Request.QueryString = new QueryString(queryString);
        }
        return context;
    }

    [Theory]
    [InlineData("access_token")]
    [InlineData("api_key")]
    public void Extract_QueryStringCredential_OnHubPath_IsAccepted(string parameter)
    {
        var context = Context("/hubs/tasks", $"?{parameter}=condt_key");

        VirtualKeyExtractor.Extract(context).Should().Be("condt_key");
    }

    [Theory]
    [InlineData("/v1/chat/completions")]
    [InlineData("/v1/embeddings")]
    [InlineData("/v1/conduit/media/file")]
    public void Extract_QueryStringCredential_OffHubPath_IsRejected(string path)
    {
        // Query strings land in access logs and referrers; only the SignalR WebSocket
        // handshake (which cannot set headers from a browser) may use them.
        var context = Context(path, "?access_token=condt_key&api_key=condt_key");

        VirtualKeyExtractor.Extract(context).Should().BeNull();
    }

    [Fact]
    public void Extract_BearerHeader_IsAccepted_OnAnyPath()
    {
        var context = Context("/v1/chat/completions");
        context.Request.Headers.Authorization = "Bearer condt_key";

        VirtualKeyExtractor.Extract(context).Should().Be("condt_key");
    }

    [Fact]
    public void Extract_ApiKeyHeader_IsAccepted_AndTrimmed()
    {
        var context = Context("/v1/embeddings");
        context.Request.Headers["X-API-Key"] = " condt_key ";

        VirtualKeyExtractor.Extract(context).Should().Be("condt_key");
    }

    [Fact]
    public void Extract_CustomConfiguredHeader_IsAccepted()
    {
        var context = Context("/v1/embeddings");
        context.Request.Headers["X-Conduit-Key"] = " condt_custom ";

        VirtualKeyExtractor.Extract(context, ["X-Conduit-Key"]).Should().Be("condt_custom");
    }

    [Fact]
    public void Extract_UnconfiguredDefaultHeader_IsRejected()
    {
        var context = Context("/v1/embeddings");
        context.Request.Headers["X-API-Key"] = "condt_default";

        VirtualKeyExtractor.Extract(context, ["X-Conduit-Key"]).Should().BeNull();
    }

    [Fact]
    public void Extract_OnHubPath_QueryStringWins_OverHeaders()
    {
        var context = Context("/hubs/tasks", "?access_token=from_query");
        context.Request.Headers.Authorization = "Bearer from_header";

        VirtualKeyExtractor.Extract(context).Should().Be("from_query");
    }

    [Fact]
    public void Extract_NullContext_ReturnsNull()
    {
        VirtualKeyExtractor.Extract(null).Should().BeNull();
    }
}

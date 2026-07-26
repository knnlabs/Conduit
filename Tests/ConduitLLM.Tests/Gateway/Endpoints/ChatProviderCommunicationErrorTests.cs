using System.Net;
using ConduitLLM.Gateway.Endpoints;

namespace ConduitLLM.Tests.Gateway.Endpoints;

public class ChatProviderCommunicationErrorTests
{
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, 429, "rate_limit_exceeded", "rate_limit_error", "rate_limited")]
    [InlineData(HttpStatusCode.BadRequest, 400, "provider_request_error", "invalid_request_error", "provider_rejected")]
    [InlineData(HttpStatusCode.NotFound, 404, "provider_request_error", "invalid_request_error", "provider_rejected")]
    [InlineData(HttpStatusCode.Unauthorized, 502, "provider_authentication_error", "server_error", "provider_error")]
    [InlineData(HttpStatusCode.Forbidden, 502, "provider_authentication_error", "server_error", "provider_error")]
    [InlineData(HttpStatusCode.RequestTimeout, 503, "provider_timeout", "server_error", "provider_unavailable")]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503, "provider_unavailable", "server_error", "provider_unavailable")]
    [InlineData(HttpStatusCode.GatewayTimeout, 504, "provider_timeout", "server_error", "provider_unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, 502, "provider_communication_error", "server_error", "provider_error")]
    public void MapProviderCommunicationError_PreservesApiSemantics(
        HttpStatusCode upstreamStatus,
        int expectedStatus,
        string expectedCode,
        string expectedType,
        string expectedMetricOutcome)
    {
        var result = ChatEndpoints.MapProviderCommunicationError(upstreamStatus);

        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedCode, result.Code);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(expectedMetricOutcome, result.MetricOutcome);
    }

    [Fact]
    public void MapProviderCommunicationError_WithoutStatusUsesBadGateway()
    {
        var result = ChatEndpoints.MapProviderCommunicationError(null);

        Assert.Equal(502, result.StatusCode);
        Assert.Equal("provider_communication_error", result.Code);
    }

    [Fact]
    public void TryExtractFileAnnotationMetadata_ReturnsOnlyReusableAnnotations()
    {
        const string providerError = """
        {
          "error": {
            "message": "failed",
            "metadata": {
              "file_annotations": [{
                "type": "file",
                "file": { "hash": "abc", "content": [{ "type": "text", "text": "parsed" }] }
              }],
              "provider_secret": "do-not-forward"
            }
          }
        }
        """;

        var metadata = ChatEndpoints.TryExtractFileAnnotationMetadata(providerError);

        Assert.NotNull(metadata);
        Assert.Equal("abc", metadata.Value.GetProperty("file_annotations")[0]
            .GetProperty("file").GetProperty("hash").GetString());
        Assert.False(metadata.Value.TryGetProperty("provider_secret", out _));
    }
}

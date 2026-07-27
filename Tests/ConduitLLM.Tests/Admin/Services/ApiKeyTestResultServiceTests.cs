using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Exceptions;

using AwesomeAssertions;

namespace ConduitLLM.Tests.Admin.Services;

public class ApiKeyTestResultServiceTests
{
    [Fact]
    public void CreateErrorResponse_ConfigurationException_Should_Surface_Actionable_Message()
    {
        var ex = new ConfigurationException("Cloudflare is missing required configuration: Account ID. Provide the value in the provider settings.");

        var response = ApiKeyTestResultService.CreateErrorResponse(ex, ProviderType.Cloudflare);

        response.Result.Should().Be(ApiKeyTestResult.Configuration);
        response.Message.Should().Be(ex.Message);
    }

    [Fact]
    public void CreateErrorResponse_405_Should_Classify_As_Configuration_Not_Unknown()
    {
        // The Cloudflare failure mode: "API returned an error: 405 MethodNotAllowed - ..."
        var ex = new LLMCommunicationException("API returned an error: 405 MethodNotAllowed - GET not supported for requested URI.");

        var response = ApiKeyTestResultService.CreateErrorResponse(ex, ProviderType.Cloudflare);

        response.Result.Should().Be(ApiKeyTestResult.Configuration);
        response.Message.Should().Contain("405");
        response.Details!.StatusCode.Should().Be(405);
    }

    [Fact]
    public void CreateErrorResponse_401_Should_Still_Classify_As_InvalidKey()
    {
        var ex = new LLMCommunicationException("API returned an error: 401 Unauthorized");

        var response = ApiKeyTestResultService.CreateErrorResponse(ex, ProviderType.OpenAI);

        response.Result.Should().Be(ApiKeyTestResult.InvalidKey);
    }

    [Fact]
    public void CreateErrorResponse_NonTestableProvider_Should_Be_Ignored()
    {
        var ex = new LLMCommunicationException("anything");

        var response = ApiKeyTestResultService.CreateErrorResponse(ex, ProviderType.Replicate);

        response.Result.Should().Be(ApiKeyTestResult.Ignored);
    }
}

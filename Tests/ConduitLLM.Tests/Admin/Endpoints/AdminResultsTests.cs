using System.Text.Json;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Endpoints;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Tests.Admin.Endpoints;

/// <summary>
/// Pins the #1262 contract fix: explicit AdminResults error returns carry the documented
/// x-request-id header, and the body TraceId matches it (both from HttpContext.TraceIdentifier,
/// the same source the exception middleware uses).
/// </summary>
[Trait("Category", "Unit")]
public class AdminResultsTests
{
    private static async Task<(DefaultHttpContext Context, AdminProblemDetails Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            TraceIdentifier = "test-correlation-id"
        };
        using var bodyStream = new MemoryStream();
        context.Response.Body = bodyStream;

        await result.ExecuteAsync(context);

        bodyStream.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<AdminProblemDetails>(
            bodyStream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return (context, body!);
    }

    [Fact]
    public async Task Problem_SetsRequestIdHeader_MatchingBodyTraceId()
    {
        var (context, body) = await ExecuteAsync(AdminResults.NotFound("Thing not found"));

        context.Response.Headers["x-request-id"].ToString().Should().Be("test-correlation-id");
        body.TraceId.Should().Be("test-correlation-id");
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task Problem_ExplicitTraceId_WinsAndStaysConsistent()
    {
        var (context, body) = await ExecuteAsync(
            AdminResults.Problem(StatusCodes.Status409Conflict, "Conflict", "conflict", traceId: "explicit-id"));

        context.Response.Headers["x-request-id"].ToString().Should().Be("explicit-id");
        body.TraceId.Should().Be("explicit-id");
    }

    [Fact]
    public void Problem_ReportsStatusCode_ForEndpointMetadata()
    {
        var result = AdminResults.Conflict("duplicate");

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }
}

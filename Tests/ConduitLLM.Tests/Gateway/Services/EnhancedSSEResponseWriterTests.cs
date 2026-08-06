using System.Text;

using ConduitLLM.Gateway.Options;
using ConduitLLM.Gateway.Services;

using Microsoft.AspNetCore.Http;

namespace ConduitLLM.Tests.Http.Services;

public class EnhancedSSEResponseWriterTests
{
    [Fact]
    public async Task WriteDone_IsIdempotentAndUsesStreamingSafeHeaders()
    {
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        context.Request.Headers.Origin = "https://example.test";
        var writer = new EnhancedSSEResponseWriter(
            context.Response,
            GatewayJsonOptions.Create());

        await writer.WriteDoneEventAsync();
        await writer.WriteDoneEventAsync();

        Assert.True(writer.HasStarted);
        Assert.Equal("text/event-stream", context.Response.ContentType);
        Assert.Equal("no-cache, no-transform", context.Response.Headers.CacheControl);
        Assert.Equal("no", context.Response.Headers["X-Accel-Buffering"]);
        Assert.False(context.Response.Headers.ContainsKey("Connection"));
        Assert.False(context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
        Assert.Equal("data: [DONE]\n\n", Encoding.UTF8.GetString(responseBody.ToArray()));
        Assert.Equal(1, writer.EventsWritten);
        Assert.Equal(responseBody.Length, writer.BytesWritten);
        Assert.NotNull(writer.FirstClientFlushAt);
    }
}

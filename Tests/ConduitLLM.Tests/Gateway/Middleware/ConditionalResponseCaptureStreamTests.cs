using System.Text;

using ConduitLLM.Gateway.Middleware;

using Microsoft.AspNetCore.Http;

namespace ConduitLLM.Tests.Http.Middleware;

public class ConditionalResponseCaptureStreamTests
{
    [Fact]
    public async Task SseResponse_PassesThroughOnFirstFlush()
    {
        var context = new DefaultHttpContext();
        var originalBody = new MemoryStream();
        await using var stream = new ConditionalResponseCaptureStream(context.Response, originalBody, 1024);
        context.Response.Body = stream;
        context.Response.ContentType = "text/event-stream; charset=utf-8";

        await stream.FlushAsync();
        await stream.WriteAsync("data: first\n\n"u8.ToArray());

        Assert.True(stream.IsPassthrough);
        Assert.Equal(0, stream.CapturedBody.Length);
        Assert.Equal("data: first\n\n", Encoding.UTF8.GetString(originalBody.ToArray()));
    }

    [Fact]
    public async Task JsonResponse_RemainsCapturedUntilExplicitCopy()
    {
        var context = new DefaultHttpContext();
        var originalBody = new MemoryStream();
        await using var stream = new ConditionalResponseCaptureStream(context.Response, originalBody, 1024);
        context.Response.ContentType = "application/json";

        await stream.WriteAsync("{\"ok\":true}"u8.ToArray());
        await stream.FlushAsync();

        Assert.False(stream.IsPassthrough);
        Assert.Equal(0, originalBody.Length);

        await stream.CopyCapturedBodyToOriginalAsync();
        Assert.Equal("{\"ok\":true}", Encoding.UTF8.GetString(originalBody.ToArray()));
    }

    [Fact]
    public async Task CaptureOverflow_FlushesPrefixAndPermanentlyPassesThrough()
    {
        var context = new DefaultHttpContext();
        var originalBody = new MemoryStream();
        await using var stream = new ConditionalResponseCaptureStream(context.Response, originalBody, 4);
        context.Response.ContentType = "application/json";

        await stream.WriteAsync("abc"u8.ToArray());
        await stream.WriteAsync("def"u8.ToArray());
        await stream.WriteAsync("g"u8.ToArray());

        Assert.True(stream.CaptureLimitExceeded);
        Assert.Equal(3, stream.CapturedBody.Length);
        Assert.Equal("abcdefg", Encoding.UTF8.GetString(originalBody.ToArray()));
    }

    [Fact]
    public async Task ModeCannotChangeAfterFirstWrite()
    {
        var context = new DefaultHttpContext();
        var originalBody = new MemoryStream();
        await using var stream = new ConditionalResponseCaptureStream(context.Response, originalBody, 1024);
        context.Response.ContentType = "application/json";

        await stream.WriteAsync("first"u8.ToArray());
        context.Response.ContentType = "text/event-stream";
        await stream.WriteAsync("second"u8.ToArray());

        Assert.False(stream.IsPassthrough);
        Assert.Equal(0, originalBody.Length);
        Assert.Equal("firstsecond", Encoding.UTF8.GetString(stream.CapturedBody.ToArray()));
    }
}

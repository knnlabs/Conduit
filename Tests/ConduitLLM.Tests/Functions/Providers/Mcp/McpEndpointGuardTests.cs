using ConduitLLM.Functions.Providers.Mcp;

namespace ConduitLLM.Tests.Functions.Providers.Mcp;

public class McpEndpointGuardTests
{
    [Theory]
    [InlineData("https://mcp.acme.com/sse")]
    [InlineData("http://example.org:8080/mcp")]
    [InlineData("https://8.8.8.8/mcp")]
    [InlineData("https://[::ffff:8.8.8.8]/mcp")]
    public void EnsureAllowed_PermitsPublicHttpEndpoints(string url)
    {
        var ex = Record.Exception(() => McpEndpointGuard.EnsureAllowed(url, allowPrivateNetwork: false));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("http://localhost:3000/mcp")]
    [InlineData("http://127.0.0.1/mcp")]
    [InlineData("http://10.0.0.5/mcp")]
    [InlineData("http://192.168.1.10/mcp")]
    [InlineData("http://172.16.4.4/mcp")]
    [InlineData("http://169.254.1.1/mcp")]
    [InlineData("http://[::ffff:127.0.0.1]/mcp")]
    [InlineData("http://[::ffff:10.0.0.5]/mcp")]
    [InlineData("http://[::ffff:172.16.4.4]/mcp")]
    [InlineData("http://[::ffff:192.168.1.10]/mcp")]
    [InlineData("http://[::ffff:169.254.169.254]/mcp")]
    [InlineData("http://internal.localhost/mcp")]
    public void EnsureAllowed_BlocksPrivateAndLoopbackByDefault(string url)
    {
        Assert.Throws<InvalidOperationException>(
            () => McpEndpointGuard.EnsureAllowed(url, allowPrivateNetwork: false));
    }

    [Theory]
    [InlineData("http://localhost:3000/mcp")]
    [InlineData("http://10.0.0.5/mcp")]
    public void EnsureAllowed_AllowsPrivateWhenOptedIn(string url)
    {
        var ex = Record.Exception(() => McpEndpointGuard.EnsureAllowed(url, allowPrivateNetwork: true));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("ftp://mcp.acme.com")]
    [InlineData("ws://mcp.acme.com")]
    [InlineData("not-a-url")]
    [InlineData("")]
    [InlineData(null)]
    public void EnsureAllowed_RejectsNonHttpOrInvalidUrls(string? url)
    {
        Assert.Throws<InvalidOperationException>(
            () => McpEndpointGuard.EnsureAllowed(url, allowPrivateNetwork: true));
    }
}

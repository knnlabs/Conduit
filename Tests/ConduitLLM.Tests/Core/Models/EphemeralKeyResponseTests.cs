using System.Text.Json;
using ConduitLLM.Admin.Models;
using ConduitLLM.Gateway.Models;

namespace ConduitLLM.Tests.Core.Models;

public sealed class EphemeralKeyResponseTests
{
    [Fact]
    public void GatewayResponse_PreservesWirePropertyNames()
    {
        var json = JsonSerializer.Serialize(
            new EphemeralKeyResponse
            {
                EphemeralKey = "gateway-token",
                ExpiresAt = DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                ExpiresInSeconds = 300,
            },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("gateway-token", root.GetProperty("ephemeral_key").GetString());
        Assert.Equal(300, root.GetProperty("expires_in_seconds").GetInt32());
        Assert.False(root.TryGetProperty("Token", out _));
    }

    [Fact]
    public void AdminResponse_PreservesWirePropertyNames()
    {
        var json = JsonSerializer.Serialize(
            new EphemeralMasterKeyResponse
            {
                EphemeralMasterKey = "admin-token",
                ExpiresAt = DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                ExpiresInSeconds = 300,
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            "admin-token",
            root.GetProperty("ephemeralMasterKey").GetString());
        Assert.Equal(300, root.GetProperty("expiresInSeconds").GetInt32());
        Assert.False(root.TryGetProperty("Token", out _));
    }
}

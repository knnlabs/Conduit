using Xunit;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.AdminClient.Tests;

public class BasicTests
{
    [Fact]
    public void ConduitAdminClientConfiguration_WithValidData_InitializesCorrectly()
    {
        // Arrange & Act
        var config = new ConduitAdminClientConfiguration
        {
            MasterKey = "test-master-key",
            AdminApiUrl = "https://admin.api.example.com"
        };

        // Assert
        Assert.Equal("test-master-key", config.MasterKey);
        Assert.Equal("https://admin.api.example.com", config.AdminApiUrl);
    }

    [Fact]
    public void ConduitAdminClient_WithValidConfiguration_InitializesSuccessfully()
    {
        // Arrange
        var configuration = new ConduitAdminClientConfiguration
        {
            MasterKey = "test-master-key",
            AdminApiUrl = "https://admin.api.example.com"
        };

        // Act
        var client = new ConduitAdminClient(configuration);

        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.VirtualKeys);
        Assert.NotNull(client.Providers);
        Assert.NotNull(client.Analytics);
        Assert.NotNull(client.Discovery);
        Assert.NotNull(client.Settings);
    }

    [Fact]
    public void PaginatedResponse_WithItems_ReturnsCorrectCount()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3" };
        var response = new PaginatedResponse<string>
        {
            Items = items,
            TotalCount = 3,
            PageNumber = 1,
            PageSize = 10
        };

        // Act & Assert
        Assert.Equal(3, response.Items.Count());
        Assert.Equal(3, response.TotalCount);
        Assert.Equal(1, response.PageNumber);
        Assert.Equal(10, response.PageSize);
    }

    [Fact]
    public void Configuration_Validation_Works()
    {
        // Arrange
        var config = new ConduitAdminClientConfiguration
        {
            MasterKey = "test-key",
            AdminApiUrl = "https://admin.example.com",
            TimeoutSeconds = 30,
            MaxRetries = 3
        };

        // Act & Assert
        Assert.Equal(30, config.TimeoutSeconds);
        Assert.Equal(3, config.MaxRetries);
    }
}
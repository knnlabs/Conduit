using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ConduitLLM.IntegrationTests.Core;

public class TestConfiguration
{
    public EnvironmentConfig Environment { get; set; } = new();
    public DefaultsConfig Defaults { get; set; } = new();
    public List<string> ActiveProviders { get; set; } = new();
}

public class EnvironmentConfig
{
    public string CoreApiUrl { get; set; } = "http://localhost:5000";
    public string AdminApiUrl { get; set; } = "http://localhost:5002";
    public string AdminApiKey { get; set; } = "";
    public S3Config S3 { get; set; } = new();
    public TimeoutConfig Timeouts { get; set; } = new();
}

public class S3Config
{
    public string Endpoint { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string Region { get; set; } = "us-east-1";
}

public class TimeoutConfig
{
    public int Default { get; set; } = 10;
    public int Chat { get; set; } = 60;
    public int ImageGen { get; set; } = 120;
    public int VideoGen { get; set; } = 300;
}

public class DefaultsConfig
{
    public decimal VirtualKeyCredit { get; set; } = 100.00m;
    public string TestPrefix { get; set; } = "TEST_";
}

// Provider configuration
public class ProviderConfig
{
    public ProviderInfo Provider { get; set; } = new();
    public List<ModelConfig> Models { get; set; } = new();
    public TestCaseConfig TestCases { get; set; } = new();
}

public class ProviderInfo
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "";
}

public class ModelConfig
{
    public string Alias { get; set; } = "";
    public string Actual { get; set; } = "";
    public ModelCapabilities Capabilities { get; set; } = new();
    public ModelCost Cost { get; set; } = new();
}

public class ModelCapabilities
{
    public bool Chat { get; set; }
    public bool Multimodal { get; set; }
    public bool Streaming { get; set; }
}

public class ModelCost
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
}

public class TestCaseConfig
{
    public ChatTestCase BasicChat { get; set; } = new();
}

public class ChatTestCase
{
    public string Prompt { get; set; } = "";
    public ValidationConfig Validation { get; set; } = new();
}

public class ValidationConfig
{
    public int MinInputTokens { get; set; }
    public int MinOutputTokens { get; set; }
    public List<string> RequiredPatterns { get; set; } = new();
}

// Test context for sharing state between test steps
public class TestContext
{
    public string TestRunId { get; set; } = $"TEST_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
    public int? ProviderId { get; set; }
    public string? ProviderKeyId { get; set; }
    public int? ModelMappingId { get; set; }
    public string? ModelAlias { get; set; }  // Store the unique model alias
    public int? ModelCostId { get; set; }
    public int? VirtualKeyGroupId { get; set; }
    public string? VirtualKey { get; set; }
    public ChatResponse? LastChatResponse { get; set; }
    public decimal InitialCredit { get; set; }
    public decimal RemainingCredit { get; set; }
    public List<string> Errors { get; set; } = new();
    
    public void SaveToFile(string path = "test-context.json")
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        File.WriteAllText(path, json);
    }
    
    public static TestContext LoadFromFile(string path = "test-context.json")
    {
        if (!File.Exists(path))
            return new TestContext();
            
        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<TestContext>(json) ?? new TestContext();
    }
}

public class ChatResponse
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? Content { get; set; }
    public decimal? InputCost { get; set; }
    public decimal? OutputCost { get; set; }
    public decimal? TotalCost { get; set; }
}

// Configuration loader
public static class ConfigurationLoader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
    
    public static TestConfiguration LoadMainConfig(string path = "Config/test-config.yaml")
    {
        // Try to resolve path relative to the test assembly location
        if (!Path.IsPathRooted(path))
        {
            var assemblyLocation = Path.GetDirectoryName(typeof(ConfigurationLoader).Assembly.Location);
            path = Path.Combine(assemblyLocation ?? ".", path);
        }
        
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Configuration file not found at {path}. " +
                "Please copy test-config.template.yaml to test-config.yaml and configure it. " +
                "When running from solution root, use: cd ConduitLLM.IntegrationTests && dotnet test");
        }
        
        var yaml = File.ReadAllText(path);
        return YamlDeserializer.Deserialize<TestConfiguration>(yaml) ?? new TestConfiguration();
    }
    
    public static ProviderConfig LoadProviderConfig(string providerName)
    {
        var path = $"Config/providers/{providerName}.yaml";
        
        // Try to resolve path relative to the test assembly location
        if (!Path.IsPathRooted(path))
        {
            var assemblyLocation = Path.GetDirectoryName(typeof(ConfigurationLoader).Assembly.Location);
            path = Path.Combine(assemblyLocation ?? ".", path);
        }
        
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Provider configuration not found at {path}. " +
                $"Please copy {providerName}.template.yaml to {providerName}.yaml and configure it.");
        }
        
        var yaml = File.ReadAllText(path);
        return YamlDeserializer.Deserialize<ProviderConfig>(yaml) ?? new ProviderConfig();
    }
    
    public static string? GetAdminApiKeyFromDockerCompose()
    {
        try
        {
            var dockerComposePath = Path.Combine("..", "docker-compose.dev.yml");
            if (File.Exists(dockerComposePath))
            {
                var content = File.ReadAllText(dockerComposePath);
                var match = Regex.Match(content, @"CONDUIT_API_TO_API_BACKEND_AUTH_KEY:\s*(.+)");
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim().Trim('"');
                }
            }
        }
        catch
        {
            // Ignore errors, will fallback to config or env var
        }
        return null;
    }
}
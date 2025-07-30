using System.Security.Cryptography;
using System.Text;

namespace ConduitLLM.IntegrationTests.Infrastructure;

/// <summary>
/// Simple builder for creating test data without entity dependencies.
/// </summary>
public class SimpleTestDataBuilder
{
    private readonly Random _random = new();

    /// <summary>
    /// Generates a random API key.
    /// </summary>
    public string GenerateApiKey()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return $"ck-{Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 48)}";
    }

    /// <summary>
    /// Hashes an API key using SHA256.
    /// </summary>
    public string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Creates test request data.
    /// </summary>
    public object CreateChatRequest(string model = "gpt-3.5-turbo", string message = "Test message")
    {
        return new
        {
            model = model,
            messages = new[]
            {
                new { role = "user", content = message }
            },
            temperature = 0.7,
            max_tokens = 100
        };
    }

    /// <summary>
    /// Creates test virtual key data.
    /// </summary>
    public (string apiKey, string keyHash, object keyData) CreateVirtualKeyData(string name = null, decimal? maxBudget = null)
    {
        var apiKey = GenerateApiKey();
        var keyHash = HashApiKey(apiKey);
        
        var keyData = new
        {
            Id = _random.Next(10000, 99999),
            KeyName = name ?? $"Test Key {_random.Next(1000)}",
            KeyHash = keyHash,
            MaxBudget = maxBudget ?? 100m,
            IsEnabled = true
        };

        return (apiKey, keyHash, keyData);
    }
}
using System.Text;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.TUI.Configuration;
using ConduitLLM.TUI.Models;

namespace ConduitLLM.TUI.Services;

public class CoreApiService : IDisposable
{
    private ConduitCoreClient? _coreClient;
    private readonly AppConfiguration _config;
    private readonly StateManager _stateManager;
    private readonly ILogger<CoreApiService> _logger;
    private string? _currentApiKey;

    public CoreApiService(AppConfiguration config, StateManager stateManager, ILogger<CoreApiService> logger)
    {
        _config = config;
        _stateManager = stateManager;
        _logger = logger;
        
        // Subscribe to virtual key changes
        _stateManager.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(StateManager.SelectedVirtualKey))
            {
                // Reset the client when virtual key changes
                _currentApiKey = null;
                _coreClient = null;
            }
        };
    }

    private ConduitCoreClient GetClient()
    {
        EnsureVirtualKeySet();
        
        // Check if we need to create a new client or if the API key has changed
        if (_coreClient == null || _currentApiKey != _stateManager.SelectedVirtualKey)
        {
            // Dispose of the old client if it exists
            _coreClient?.Dispose();
            
            _currentApiKey = _stateManager.SelectedVirtualKey;
            
            // Create a new client with the selected virtual key as the API key
            var configuration = new ConduitCoreClientConfiguration
            {
                ApiKey = _currentApiKey,
                BaseUrl = _config.CoreApiUrl
            };
            
            _coreClient = new ConduitCoreClient(configuration);
            _logger.LogInformation("Created new CoreClient with virtual key: {KeyPrefix}...", 
                _currentApiKey?.Substring(0, Math.Min(8, _currentApiKey.Length)));
        }
        
        return _coreClient;
    }

    private void EnsureVirtualKeySet()
    {
        if (string.IsNullOrEmpty(_stateManager.SelectedVirtualKey))
        {
            throw new InvalidOperationException("No virtual key selected. Please select a virtual key first.");
        }
    }

    // Chat Completions
    public async IAsyncEnumerable<string> CreateChatCompletionStreamAsync(ChatCompletionRequest request)
    {
        var client = GetClient();
        
        var stream = client.Chat.CreateCompletionStreamAsync(request);
        await foreach (var chunk in stream)
        {
            var content = chunk.Choices?.FirstOrDefault()?.Delta?.Content;
            if (content != null)
            {
                yield return content;
            }
        }
    }

    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request)
    {
        try
        {
            var client = GetClient();
            return await client.Chat.CreateCompletionAsync(request);
        }
        catch (ConduitLLM.CoreClient.Exceptions.ConduitCoreException coreEx) when (coreEx.StatusCode == 403)
        {
            _logger.LogError("Authorization failed for chat completion: {Message}", coreEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.AuthenticationException authEx)
        {
            _logger.LogError("Authentication failed for chat completion: {Message}", authEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.NetworkException netEx)
        {
            _logger.LogError("Network error during chat completion: {Message}", netEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create chat completion: {Message}", ex.Message);
            throw;
        }
    }

    // Image Generation
    public async Task<ImageGenerationResponse> CreateImageGenerationAsync(ImageGenerationRequest request)
    {
        try
        {
            var client = GetClient();
            return await client.Images.GenerateAsync(request);
        }
        catch (ConduitLLM.CoreClient.Exceptions.ConduitCoreException coreEx) when (coreEx.StatusCode == 403)
        {
            _logger.LogError("Authorization failed for image generation: {Message}", coreEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.AuthenticationException authEx)
        {
            _logger.LogError("Authentication failed for image generation: {Message}", authEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.NetworkException netEx)
        {
            _logger.LogError("Network error during image generation: {Message}", netEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create image generation: {Message}", ex.Message);
            throw;
        }
    }

    // Video Generation (synchronous)
    public async Task<VideoGenerationResponse> CreateVideoGenerationAsync(VideoGenerationRequest request)
    {
        try
        {
            var client = GetClient();
            return await client.Videos.GenerateAsync(request);
        }
        catch (ConduitLLM.CoreClient.Exceptions.ConduitCoreException coreEx) when (coreEx.StatusCode == 403)
        {
            _logger.LogError("Authorization failed for video generation: {Message}", coreEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.AuthenticationException authEx)
        {
            _logger.LogError("Authentication failed for video generation: {Message}", authEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.NetworkException netEx)
        {
            _logger.LogError("Network error during video generation: {Message}", netEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create video generation: {Message}", ex.Message);
            throw;
        }
    }

    // Video Generation (asynchronous)
    public async Task<AsyncVideoGenerationResponse> CreateAsyncVideoGenerationAsync(AsyncVideoGenerationRequest request)
    {
        try
        {
            var client = GetClient();
            return await client.Videos.GenerateAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create async video generation");
            throw;
        }
    }

    public async Task<AsyncVideoGenerationResponse> GetVideoGenerationStatusAsync(string taskId)
    {
        try
        {
            var client = GetClient();
            return await client.Videos.GetTaskStatusAsync(taskId);
        }
        catch (ConduitLLM.CoreClient.Exceptions.ConduitCoreException coreEx) when (coreEx.StatusCode == 403)
        {
            _logger.LogError("Authorization failed when getting video status: {Message}", coreEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.AuthenticationException authEx)
        {
            _logger.LogError("Authentication failed when getting video status: {Message}", authEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.NetworkException netEx)
        {
            _logger.LogError("Network error when getting video status: {Message}", netEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get video generation status: {Message}", ex.Message);
            throw;
        }
    }

    // Models
    public async Task<ModelsResponse> GetModelsAsync()
    {
        try
        {
            var client = GetClient();
            return await client.Models.ListAsync();
        }
        catch (ConduitLLM.CoreClient.Exceptions.ConduitCoreException coreEx) when (coreEx.StatusCode == 403)
        {
            _logger.LogError("Authorization failed when getting models: {Message}", coreEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.AuthenticationException authEx)
        {
            _logger.LogError("Authentication failed when getting models: {Message}", authEx.Message);
            throw;
        }
        catch (ConduitLLM.CoreClient.Exceptions.NetworkException netEx)
        {
            _logger.LogError("Network error when getting models: {Message}", netEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get models: {Message}", ex.Message);
            throw;
        }
    }

    // Navigation State
    public async Task<NavigationStateDto> GetNavigationStateAsync()
    {
        try
        {
            // Navigation state doesn't require a virtual key
            // Navigation state is not available in the SDK yet
            throw new NotImplementedException("Navigation state endpoint not available in SDK");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get navigation state: {Message}", ex.Message);
            throw;
        }
    }

    public void Dispose()
    {
        _coreClient?.Dispose();
    }
}
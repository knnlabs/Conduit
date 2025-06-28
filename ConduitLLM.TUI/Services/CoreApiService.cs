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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat completion");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create image generation");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create video generation");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get video generation status");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get models");
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
            _logger.LogError(ex, "Failed to get navigation state");
            throw;
        }
    }

    public void Dispose()
    {
        _coreClient?.Dispose();
    }
}
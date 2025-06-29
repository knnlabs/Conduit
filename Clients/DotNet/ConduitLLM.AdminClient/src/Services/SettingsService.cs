using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Constants;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing global settings and configurations through the Admin API.
/// </summary>
public class SettingsService : BaseApiClient
{
    private const string GlobalSettingsEndpoint = "/api/GlobalSettings";
    private const string AudioConfigEndpoint = "/api/audio-configuration";
    private const string RouterConfigEndpoint = "/api/router-configuration";
    private const int DefaultPageSize = 50;
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MediumCacheTimeout = TimeSpan.FromMinutes(3);
    private static readonly Regex KeyValidationRegex = new(@"^[A-Z_][A-Z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the SettingsService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public SettingsService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<SettingsService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region Global Settings

    /// <summary>
    /// Retrieves global settings with optional filtering.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of global settings matching the filter criteria.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<GlobalSettingDto>> GetGlobalSettingsAsync(
        SettingFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = BuildSettingFilterParameters(filters);
            var cacheKey = GetCacheKey("global-settings", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<GlobalSettingDto>>(GlobalSettingsEndpoint, parameters, cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, GlobalSettingsEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific global setting by key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The global setting information.</returns>
    /// <exception cref="ValidationException">Thrown when the key is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the setting is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<GlobalSettingDto> GetGlobalSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ValidationException("Setting key is required");

            var endpoint = $"{GlobalSettingsEndpoint}/by-key/{Uri.EscapeDataString(key)}";
            var cacheKey = GetCacheKey("global-setting", key);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<GlobalSettingDto>(endpoint, cancellationToken: cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{GlobalSettingsEndpoint}/{key}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Creates a new global setting.
    /// </summary>
    /// <param name="request">The global setting creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created global setting.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<GlobalSettingDto> CreateGlobalSettingAsync(
        CreateGlobalSettingDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateCreateGlobalSettingRequest(request);

            var response = await PostAsync<GlobalSettingDto>(
                GlobalSettingsEndpoint,
                request,
                cancellationToken);

            await InvalidateCacheAsync();
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, GlobalSettingsEndpoint, "POST");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing global setting.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the key or request is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the setting is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateGlobalSettingAsync(
        string key,
        UpdateGlobalSettingDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ValidationException("Setting key is required");

            if (request == null)
                throw new ValidationException("Update request cannot be null");

            var endpoint = $"{GlobalSettingsEndpoint}/by-key/{Uri.EscapeDataString(key)}";
            await PutAsync(endpoint, request, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{GlobalSettingsEndpoint}/{key}", "PUT");
            throw;
        }
    }

    /// <summary>
    /// Deletes a global setting by key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the key is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the setting is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DeleteGlobalSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ValidationException("Setting key is required");

            var endpoint = $"{GlobalSettingsEndpoint}/by-key/{Uri.EscapeDataString(key)}";
            await DeleteAsync(endpoint, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{GlobalSettingsEndpoint}/{key}", "DELETE");
            throw;
        }
    }

    #endregion

    #region Audio Configuration

    /// <summary>
    /// Retrieves all audio configurations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of audio configurations.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<AudioConfigurationDto>> GetAudioConfigurationsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("audio-configurations");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<AudioConfigurationDto>>(AudioConfigEndpoint, cancellationToken: cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, AudioConfigEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves audio configuration for a specific provider.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audio configuration for the specified provider.</returns>
    /// <exception cref="ValidationException">Thrown when the provider is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the configuration is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<AudioConfigurationDto> GetAudioConfigurationAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(provider))
                throw new ValidationException("Provider is required");

            var endpoint = $"{AudioConfigEndpoint}/{Uri.EscapeDataString(provider)}";
            var cacheKey = GetCacheKey("audio-config", provider);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<AudioConfigurationDto>(endpoint, cancellationToken: cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{AudioConfigEndpoint}/{provider}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Creates a new audio configuration.
    /// </summary>
    /// <param name="request">The audio configuration creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created audio configuration.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<AudioConfigurationDto> CreateAudioConfigurationAsync(
        CreateAudioConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateCreateAudioConfigurationRequest(request);

            var response = await PostAsync<AudioConfigurationDto>(
                AudioConfigEndpoint,
                request,
                cancellationToken);

            await InvalidateCacheAsync();
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, AudioConfigEndpoint, "POST");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing audio configuration.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the provider or request is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the configuration is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateAudioConfigurationAsync(
        string provider,
        UpdateAudioConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(provider))
                throw new ValidationException("Provider is required");

            if (request == null)
                throw new ValidationException("Update request cannot be null");

            var endpoint = $"{AudioConfigEndpoint}/{Uri.EscapeDataString(provider)}";
            await PutAsync(endpoint, request, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{AudioConfigEndpoint}/{provider}", "PUT");
            throw;
        }
    }

    /// <summary>
    /// Deletes an audio configuration.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the provider is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the configuration is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DeleteAudioConfigurationAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(provider))
                throw new ValidationException("Provider is required");

            var endpoint = $"{AudioConfigEndpoint}/{Uri.EscapeDataString(provider)}";
            await DeleteAsync(endpoint, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{AudioConfigEndpoint}/{provider}", "DELETE");
            throw;
        }
    }

    #endregion

    #region Router Configuration

    /// <summary>
    /// Retrieves the router configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The router configuration.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<RouterConfigurationDto> GetRouterConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("router-configuration");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<RouterConfigurationDto>(RouterConfigEndpoint, cancellationToken: cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, RouterConfigEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Updates the router configuration.
    /// </summary>
    /// <param name="request">The router configuration update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateRouterConfigurationAsync(
        UpdateRouterConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
                throw new ValidationException("Update request cannot be null");

            await PutAsync(RouterConfigEndpoint, request, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, RouterConfigEndpoint, "PUT");
            throw;
        }
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The setting value.</returns>
    /// <exception cref="ValidationException">Thrown when the key is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the setting is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<string> GetSettingValueAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var setting = await GetGlobalSettingAsync(key, cancellationToken);
        return setting.Value;
    }

    /// <summary>
    /// Sets a setting value, creating it if it doesn't exist or updating it if it does.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The setting value.</param>
    /// <param name="options">Optional setting configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task SetSettingAsync(
        string key,
        string value,
        SettingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get existing setting
            await GetGlobalSettingAsync(key, cancellationToken);
            
            // Setting exists, update it
            await UpdateGlobalSettingAsync(key, new UpdateGlobalSettingDto
            {
                Value = value,
                Description = options?.Description,
                Category = options?.Category
            }, cancellationToken);
        }
        catch (NotFoundException)
        {
            // Setting doesn't exist, create it
            await CreateGlobalSettingAsync(new CreateGlobalSettingDto
            {
                Key = key,
                Value = value,
                Description = options?.Description,
                DataType = options?.DataType ?? SettingDataType.String,
                Category = options?.Category,
                IsSecret = options?.IsSecret ?? false
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Retrieves all settings in a specific category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of settings in the specified category.</returns>
    /// <exception cref="ValidationException">Thrown when the category is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<GlobalSettingDto>> GetSettingsByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ValidationException("Category is required");

        var filters = new SettingFilters { Category = category };
        return await GetGlobalSettingsAsync(filters, cancellationToken);
    }

    #endregion

    #region System Configuration

    /// <summary>
    /// Retrieves the complete system configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete system configuration.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<SystemConfiguration> GetSystemConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = "/settings/system-configuration";
            var cacheKey = GetCacheKey("system-configuration");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<SystemConfiguration>(endpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, "/settings/system-configuration", "GET");
            throw;
        }
    }

    /// <summary>
    /// Exports settings in the specified format.
    /// </summary>
    /// <param name="format">The export format (json or env).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the exported settings.</returns>
    /// <exception cref="ValidationException">Thrown when the format is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<Stream> ExportSettingsAsync(
        string format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var supportedFormats = new[] { "json", "env" };
            if (!supportedFormats.Contains(format?.ToLower()))
                throw new ValidationException($"Unsupported format: {format}. Supported formats: {string.Join(", ", supportedFormats)}");

            var endpoint = "/settings/export";
            var response = await HttpClient.GetAsync($"{endpoint}?format={Uri.EscapeDataString(format.ToLower())}", cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(response, endpoint, "GET");

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, "/settings/export", "GET");
            throw;
        }
    }

    /// <summary>
    /// Imports settings from a file.
    /// </summary>
    /// <param name="fileStream">The file stream containing the settings.</param>
    /// <param name="format">The file format (json or env).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import results.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<SettingsImportResult> ImportSettingsAsync(
        Stream fileStream,
        string format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (fileStream == null)
                throw new ValidationException("File stream is required");

            var supportedFormats = new[] { "json", "env" };
            if (!supportedFormats.Contains(format?.ToLower()))
                throw new ValidationException($"Unsupported format: {format}. Supported formats: {string.Join(", ", supportedFormats)}");

            var endpoint = "/settings/import";
            
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", $"settings.{format.ToLower()}");
            content.Add(new StringContent(format.ToLower()), "format");

            var response = await HttpClient.PostAsync(endpoint, content, cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(response, endpoint, "POST");

            var result = await response.Content.ReadFromJsonAsync<SettingsImportResult>(cancellationToken: cancellationToken);
            await InvalidateCacheAsync();
            
            return result ?? throw new ConduitAdminException("Invalid response from server", null, null, null, null);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, "/settings/import", "POST");
            throw;
        }
    }

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation results.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = "/settings/validate";
            return await PostAsync<ConfigurationValidationResult>(endpoint, new { }, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, "/settings/validate", "POST");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private static void ValidateCreateGlobalSettingRequest(CreateGlobalSettingDto request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (string.IsNullOrWhiteSpace(request.Key))
            throw new ValidationException("Setting key is required");

        if (!KeyValidationRegex.IsMatch(request.Key))
            throw new ValidationException("Key must be uppercase with underscores (e.g., 'API_TIMEOUT')");

        if (string.IsNullOrWhiteSpace(request.Value))
            throw new ValidationException("Setting value is required");
    }

    private static void ValidateCreateAudioConfigurationRequest(CreateAudioConfigurationDto request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (string.IsNullOrWhiteSpace(request.Provider))
            throw new ValidationException("Provider is required");

        if (!string.IsNullOrEmpty(request.ApiEndpoint) && !Uri.TryCreate(request.ApiEndpoint, UriKind.Absolute, out _))
            throw new ValidationException("API endpoint must be a valid URL");

        if (request.MaxDuration.HasValue && request.MaxDuration <= 0)
            throw new ValidationException("Max duration must be positive");
    }

    private object BuildSettingFilterParameters(SettingFilters? filters)
    {
        if (filters == null)
        {
            return new { pageNumber = 1, pageSize = DefaultPageSize };
        }

        return new
        {
            pageNumber = filters.PageNumber ?? 1,
            pageSize = filters.PageSize ?? DefaultPageSize,
            search = filters.Search,
            sortBy = filters.SortBy?.Field,
            sortDirection = filters.SortBy?.Direction.ToString().ToLower(),
            category = filters.Category,
            dataType = filters.DataType?.ToString().ToLower(),
            isSecret = filters.IsSecret,
            searchKey = filters.SearchKey
        };
    }

    private async Task InvalidateCacheAsync()
    {
        // Clear all settings-related cache entries
        await Task.CompletedTask;
    }

    #endregion

    #region HTTP Client Configuration

    /// <summary>
    /// Retrieves the current HTTP client configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP client configuration.</returns>
    /// <exception cref="NotFoundException">Thrown when no configuration is found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<HttpClientConfigurationDto> GetHttpClientConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = ApiEndpoints.System.HttpClientConfiguration;
            var cacheKey = GetCacheKey("http-client-config");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<HttpClientConfigurationDto>(endpoint, cancellationToken: cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ApiEndpoints.System.HttpClientConfiguration, "GET");
            throw;
        }
    }

    /// <summary>
    /// Creates a new HTTP client configuration.
    /// </summary>
    /// <param name="request">The HTTP client configuration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created HTTP client configuration.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<HttpClientConfigurationDto> CreateHttpClientConfigurationAsync(
        CreateHttpClientConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
                throw new ValidationException("HTTP client configuration request is required");

            ValidateHttpClientConfiguration(request);

            var endpoint = ApiEndpoints.System.HttpClientConfiguration;
            var result = await PostAsync<HttpClientConfigurationDto>(endpoint, request, cancellationToken);

            // Clear cache after successful creation
            await InvalidateCacheAsync();

            return result;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ApiEndpoints.System.HttpClientConfiguration, "POST");
            throw;
        }
    }

    /// <summary>
    /// Updates the HTTP client configuration.
    /// </summary>
    /// <param name="request">The HTTP client configuration update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated HTTP client configuration.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<HttpClientConfigurationDto> UpdateHttpClientConfigurationAsync(
        UpdateHttpClientConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
                throw new ValidationException("HTTP client configuration update request is required");

            ValidateHttpClientConfiguration(request);

            var endpoint = $"{ApiEndpoints.System.HttpClientConfiguration}/{request.Id}";
            var result = await PutAsync<HttpClientConfigurationDto>(endpoint, request, cancellationToken);

            // Clear cache after successful update
            await InvalidateCacheAsync();

            return result;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ApiEndpoints.System.HttpClientConfiguration, "PUT");
            throw;
        }
    }

    /// <summary>
    /// Deletes the HTTP client configuration.
    /// </summary>
    /// <param name="id">The configuration ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the ID is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DeleteHttpClientConfigurationAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
                throw new ValidationException("HTTP client configuration ID must be positive");

            var endpoint = $"{ApiEndpoints.System.HttpClientConfiguration}/{id}";
            await DeleteAsync(endpoint, cancellationToken);

            // Clear cache after successful deletion
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ApiEndpoints.System.HttpClientConfiguration, "DELETE");
            throw;
        }
    }

    /// <summary>
    /// Tests HTTP client configuration settings.
    /// </summary>
    /// <param name="config">The configuration to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the configuration is valid, false otherwise.</returns>
    /// <exception cref="ValidationException">Thrown when the configuration is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<bool> TestHttpClientConfigurationAsync(
        CreateHttpClientConfigurationDto config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (config == null)
                throw new ValidationException("HTTP client configuration is required");

            ValidateHttpClientConfiguration(config);

            var endpoint = $"{ApiEndpoints.System.HttpClientConfiguration}/test";
            var result = await PostAsync<bool>(endpoint, config, cancellationToken);

            return result;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{ApiEndpoints.System.HttpClientConfiguration}/test", "POST");
            throw;
        }
    }

    #endregion

    #region Cache Configuration

    /// <summary>
    /// Retrieves the current cache configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cache configuration.</returns>
    /// <exception cref="NotFoundException">Thrown when no configuration is found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CacheConfigurationDto> GetCacheConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = ApiEndpoints.System.CacheConfiguration;
            var cacheKey = GetCacheKey("cache-config");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<CacheConfigurationDto>(endpoint, cancellationToken: cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ApiEndpoints.System.CacheConfiguration, "GET");
            throw;
        }
    }

    /// <summary>
    /// Creates a new cache configuration.
    /// </summary>
    /// <param name="request">The cache configuration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created cache configuration.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CacheConfigurationDto> CreateCacheConfigurationAsync(
        CreateCacheConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
                throw new ValidationException("Cache configuration request is required");

            ValidateCacheConfiguration(request);

            var endpoint = ApiEndpoints.System.CacheConfiguration;
            var result = await PostAsync<CacheConfigurationDto>(endpoint, request, cancellationToken);

            // Clear cache after successful creation
            await InvalidateCacheAsync();

            return result;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ApiEndpoints.System.CacheConfiguration, "POST");
            throw;
        }
    }

    /// <summary>
    /// Updates the cache configuration.
    /// </summary>
    /// <param name="request">The cache configuration update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated cache configuration.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CacheConfigurationDto> UpdateCacheConfigurationAsync(
        UpdateCacheConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
                throw new ValidationException("Cache configuration update request is required");

            ValidateCacheConfiguration(request);

            var endpoint = $"{ApiEndpoints.System.CacheConfiguration}/{request.Id}";
            var result = await PutAsync<CacheConfigurationDto>(endpoint, request, cancellationToken);

            // Clear cache after successful update
            await InvalidateCacheAsync();

            return result;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ApiEndpoints.System.CacheConfiguration, "PUT");
            throw;
        }
    }

    /// <summary>
    /// Deletes the cache configuration.
    /// </summary>
    /// <param name="id">The configuration ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the ID is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DeleteCacheConfigurationAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
                throw new ValidationException("Cache configuration ID must be positive");

            var endpoint = $"{ApiEndpoints.System.CacheConfiguration}/{id}";
            await DeleteAsync(endpoint, cancellationToken);

            // Clear cache after successful deletion
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ApiEndpoints.System.CacheConfiguration, "DELETE");
            throw;
        }
    }

    /// <summary>
    /// Tests cache configuration settings.
    /// </summary>
    /// <param name="config">The configuration to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the configuration is valid, false otherwise.</returns>
    /// <exception cref="ValidationException">Thrown when the configuration is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<bool> TestCacheConfigurationAsync(
        CreateCacheConfigurationDto config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (config == null)
                throw new ValidationException("Cache configuration is required");

            ValidateCacheConfiguration(config);

            var endpoint = $"{ApiEndpoints.System.CacheConfiguration}/test";
            var result = await PostAsync<bool>(endpoint, config, cancellationToken);

            return result;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{ApiEndpoints.System.CacheConfiguration}/test", "POST");
            throw;
        }
    }

    #endregion

    #region Configuration Validation

    private void ValidateHttpClientConfiguration(CreateHttpClientConfigurationDto config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientName))
            throw new ValidationException("Client name is required");

        if (config.TimeoutSeconds.HasValue && config.TimeoutSeconds <= 0)
            throw new ValidationException("Timeout seconds must be positive");

        if (config.MaxRetries.HasValue && config.MaxRetries < 0)
            throw new ValidationException("Max retries must be non-negative");

        if (config.InitialDelayMs.HasValue && config.InitialDelayMs <= 0)
            throw new ValidationException("Initial delay must be positive");

        if (config.MaxDelayMs.HasValue && config.MaxDelayMs <= 0)
            throw new ValidationException("Max delay must be positive");

        if (config.MaxConnectionsPerServer.HasValue && config.MaxConnectionsPerServer <= 0)
            throw new ValidationException("Max connections per server must be positive");

        if (config.ConnectionLifetimeMinutes.HasValue && config.ConnectionLifetimeMinutes <= 0)
            throw new ValidationException("Connection lifetime must be positive");

        if (config.PooledConnectionIdleTimeoutMinutes.HasValue && config.PooledConnectionIdleTimeoutMinutes <= 0)
            throw new ValidationException("Pooled connection idle timeout must be positive");

        if (config.ConnectTimeoutSeconds.HasValue && config.ConnectTimeoutSeconds <= 0)
            throw new ValidationException("Connect timeout must be positive");
    }

    private void ValidateHttpClientConfiguration(UpdateHttpClientConfigurationDto config)
    {
        if (config.Id <= 0)
            throw new ValidationException("Configuration ID must be positive");

        if (!string.IsNullOrEmpty(config.ClientName) && string.IsNullOrWhiteSpace(config.ClientName))
            throw new ValidationException("Client name cannot be empty");

        if (config.TimeoutSeconds.HasValue && config.TimeoutSeconds <= 0)
            throw new ValidationException("Timeout seconds must be positive");

        if (config.MaxRetries.HasValue && config.MaxRetries < 0)
            throw new ValidationException("Max retries must be non-negative");

        if (config.InitialDelayMs.HasValue && config.InitialDelayMs <= 0)
            throw new ValidationException("Initial delay must be positive");

        if (config.MaxDelayMs.HasValue && config.MaxDelayMs <= 0)
            throw new ValidationException("Max delay must be positive");

        if (config.MaxConnectionsPerServer.HasValue && config.MaxConnectionsPerServer <= 0)
            throw new ValidationException("Max connections per server must be positive");

        if (config.ConnectionLifetimeMinutes.HasValue && config.ConnectionLifetimeMinutes <= 0)
            throw new ValidationException("Connection lifetime must be positive");

        if (config.PooledConnectionIdleTimeoutMinutes.HasValue && config.PooledConnectionIdleTimeoutMinutes <= 0)
            throw new ValidationException("Pooled connection idle timeout must be positive");

        if (config.ConnectTimeoutSeconds.HasValue && config.ConnectTimeoutSeconds <= 0)
            throw new ValidationException("Connect timeout must be positive");
    }

    private void ValidateCacheConfiguration(CreateCacheConfigurationDto config)
    {
        if (config.DefaultAbsoluteExpirationMinutes.HasValue && config.DefaultAbsoluteExpirationMinutes <= 0)
            throw new ValidationException("Default absolute expiration must be positive");

        if (config.DefaultSlidingExpirationMinutes.HasValue && config.DefaultSlidingExpirationMinutes <= 0)
            throw new ValidationException("Default sliding expiration must be positive");

        if (config.MaxCacheItems.HasValue && config.MaxCacheItems <= 0)
            throw new ValidationException("Max cache items must be positive");

        if (!string.IsNullOrEmpty(config.RedisConnectionString) && string.IsNullOrWhiteSpace(config.RedisConnectionString))
            throw new ValidationException("Redis connection string cannot be empty");

        if (!string.IsNullOrEmpty(config.RedisInstanceName) && string.IsNullOrWhiteSpace(config.RedisInstanceName))
            throw new ValidationException("Redis instance name cannot be empty");

        if (!string.IsNullOrEmpty(config.HashAlgorithm) && !IsValidHashAlgorithm(config.HashAlgorithm))
            throw new ValidationException("Hash algorithm must be MD5, SHA1, or SHA256");
    }

    private void ValidateCacheConfiguration(UpdateCacheConfigurationDto config)
    {
        if (config.Id <= 0)
            throw new ValidationException("Configuration ID must be positive");

        if (config.DefaultAbsoluteExpirationMinutes.HasValue && config.DefaultAbsoluteExpirationMinutes <= 0)
            throw new ValidationException("Default absolute expiration must be positive");

        if (config.DefaultSlidingExpirationMinutes.HasValue && config.DefaultSlidingExpirationMinutes <= 0)
            throw new ValidationException("Default sliding expiration must be positive");

        if (config.MaxCacheItems.HasValue && config.MaxCacheItems <= 0)
            throw new ValidationException("Max cache items must be positive");

        if (!string.IsNullOrEmpty(config.RedisConnectionString) && string.IsNullOrWhiteSpace(config.RedisConnectionString))
            throw new ValidationException("Redis connection string cannot be empty");

        if (!string.IsNullOrEmpty(config.RedisInstanceName) && string.IsNullOrWhiteSpace(config.RedisInstanceName))
            throw new ValidationException("Redis instance name cannot be empty");

        if (!string.IsNullOrEmpty(config.HashAlgorithm) && !IsValidHashAlgorithm(config.HashAlgorithm))
            throw new ValidationException("Hash algorithm must be MD5, SHA1, or SHA256");
    }

    private static bool IsValidHashAlgorithm(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "MD5" => true,
            "SHA1" => true,
            "SHA256" => true,
            _ => false
        };
    }

    #endregion
}
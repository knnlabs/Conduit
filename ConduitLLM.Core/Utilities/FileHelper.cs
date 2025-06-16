using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Helper class for common file operations with standardized error handling.
    /// </summary>
    public static class FileHelper
    {
        private static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>
        /// Safely reads and deserializes a JSON file.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the JSON to.</typeparam>
        /// <param name="filePath">The path to the JSON file.</param>
        /// <param name="options">Optional JSON serialization options.</param>
        /// <param name="logger">Optional logger for error logging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The deserialized object, or default if the file doesn't exist.</returns>
        /// <exception cref="ConfigurationException">Thrown if there's an error reading or deserializing the file.</exception>
        public static async Task<T?> ReadJsonFileAsync<T>(
            string filePath,
            JsonSerializerOptions? options = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                logger?.LogInformation("File not found at {FilePath}", filePath);
                return default;
            }

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return await JsonSerializer.DeserializeAsync<T>(
                    fileStream, options ?? DefaultJsonOptions, cancellationToken);
            }
            catch (IOException ex)
            {
                logger?.LogError(ex, "IO error reading file {FilePath}", filePath);
                throw new ConfigurationException($"Error reading file {filePath}: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                logger?.LogError(ex, "JSON parsing error in file {FilePath}", filePath);
                throw new ConfigurationException($"Error parsing JSON in {filePath}: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Unexpected error reading file {FilePath}", filePath);
                throw new ConfigurationException($"Unexpected error reading {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Safely serializes and writes an object to a JSON file.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        /// <param name="filePath">The path to write the JSON file to.</param>
        /// <param name="data">The object to serialize and write.</param>
        /// <param name="options">Optional JSON serialization options.</param>
        /// <param name="logger">Optional logger for error logging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">Thrown if data is null.</exception>
        /// <exception cref="ConfigurationException">Thrown if there's an error writing the file.</exception>
        public static async Task WriteJsonFileAsync<T>(
            string filePath,
            T data,
            JsonSerializerOptions? options = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                await JsonSerializer.SerializeAsync(fileStream, data, options ?? DefaultJsonOptions, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);

                logger?.LogInformation("Successfully wrote JSON to {FilePath}", filePath);
            }
            catch (IOException ex)
            {
                logger?.LogError(ex, "IO error writing file {FilePath}", filePath);
                throw new ConfigurationException($"Error writing file {filePath}: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Unexpected error writing file {FilePath}", filePath);
                throw new ConfigurationException($"Unexpected error writing {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Safely reads a text file with proper error handling.
        /// </summary>
        /// <param name="filePath">The path to the text file.</param>
        /// <param name="logger">Optional logger for error logging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The contents of the text file, or null if the file doesn't exist.</returns>
        /// <exception cref="ConfigurationException">Thrown if there's an error reading the file.</exception>
        public static async Task<string?> ReadTextFileAsync(
            string filePath,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                logger?.LogInformation("File not found at {FilePath}", filePath);
                return null;
            }

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(fileStream, Encoding.UTF8);
                return await reader.ReadToEndAsync();
            }
            catch (IOException ex)
            {
                logger?.LogError(ex, "IO error reading file {FilePath}", filePath);
                throw new ConfigurationException($"Error reading file {filePath}: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Unexpected error reading file {FilePath}", filePath);
                throw new ConfigurationException($"Unexpected error reading {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Safely writes text to a file with proper error handling.
        /// </summary>
        /// <param name="filePath">The path to write the text file to.</param>
        /// <param name="content">The content to write to the file.</param>
        /// <param name="logger">Optional logger for error logging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">Thrown if content is null.</exception>
        /// <exception cref="ConfigurationException">Thrown if there's an error writing the file.</exception>
        public static async Task WriteTextFileAsync(
            string filePath,
            string content,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using var writer = new StreamWriter(fileStream, Encoding.UTF8);
                await writer.WriteAsync(content);
                await writer.FlushAsync();

                logger?.LogInformation("Successfully wrote to {FilePath}", filePath);
            }
            catch (IOException ex)
            {
                logger?.LogError(ex, "IO error writing file {FilePath}", filePath);
                throw new ConfigurationException($"Error writing file {filePath}: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Unexpected error writing file {FilePath}", filePath);
                throw new ConfigurationException($"Unexpected error writing {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Safely checks if a file exists.
        /// </summary>
        /// <param name="filePath">The path to the file to check.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
        public static bool FileExists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                // File.Exists can throw for invalid paths or security issues
                // Return false instead of propagating the exception
                // Logger is not available in this static method without changing the signature
                System.Diagnostics.Debug.WriteLine($"Error checking file existence for '{filePath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Safely ensures a directory exists, creating it if necessary.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to ensure exists.</param>
        /// <param name="logger">Optional logger for error logging.</param>
        /// <returns>True if the directory exists or was created, false otherwise.</returns>
        public static bool EnsureDirectoryExists(string directoryPath, ILogger? logger = null)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                return false;
            }

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    logger?.LogInformation("Created directory {DirectoryPath}", directoryPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error creating directory {DirectoryPath}", directoryPath);
                return false;
            }
        }

        /// <summary>
        /// Safely deletes a file if it exists.
        /// </summary>
        /// <param name="filePath">The path to the file to delete.</param>
        /// <param name="logger">Optional logger for error logging.</param>
        /// <returns>True if the file was deleted or didn't exist, false if an error occurred.</returns>
        public static bool DeleteFileIfExists(string filePath, ILogger? logger = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    logger?.LogInformation("Deleted file {FilePath}", filePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error deleting file {FilePath}", filePath);
                return false;
            }
        }
    }
}

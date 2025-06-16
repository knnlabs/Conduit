using System;
using System.Collections.Concurrent;
using System.IO;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for generating cache-busting version strings for static files.
    /// </summary>
    public interface IFileVersionService
    {
        /// <summary>
        /// Gets a version string for the specified file path.
        /// </summary>
        string GetFileVersion(string filePath);

        /// <summary>
        /// Gets the application version.
        /// </summary>
        string AppVersion { get; }
    }

    /// <summary>
    /// Implementation of file version service using file modification time and app version.
    /// </summary>
    public class FileVersionService : IFileVersionService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly string _appVersion;
        private readonly ConcurrentDictionary<string, string> _versionCache = new();

        public FileVersionService(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _appVersion = configuration["AppVersion"] ?? GetAssemblyVersion();
        }

        public string AppVersion => _appVersion;

        public string GetFileVersion(string filePath)
        {
            // In production, use app version for better caching
            if (!_environment.IsDevelopment())
            {
                return _appVersion;
            }

            // In development, use file modification time for immediate updates
            return _versionCache.GetOrAdd(filePath, path =>
            {
                var fileInfo = _environment.WebRootFileProvider.GetFileInfo(path);
                if (fileInfo.Exists)
                {
                    return fileInfo.LastModified.UtcTicks.ToString("x");
                }
                return _appVersion;
            });
        }

        private string GetAssemblyVersion()
        {
            var assembly = typeof(FileVersionService).Assembly;
            var version = assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
    }
}

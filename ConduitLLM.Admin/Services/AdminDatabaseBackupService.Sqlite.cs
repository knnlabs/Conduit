using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for database backup functionality - SQLite operations and helpers
    /// </summary>
    public partial class AdminDatabaseBackupService
    {
        private async Task<RestoreResult> RestoreSqliteBackupAsync(string backupFilePath)
        {
            try
            {
                // Get the database connection string
                var connectionString = _dbContext.GetDatabase().GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    return new RestoreResult
                    {
                        Success = false,
                        ErrorMessage = "Database connection string is empty"
                    };
                }

                // Extract the database file path from the connection string
                string dbFilePath;
                if (connectionString.Contains("Data Source="))
                {
                    dbFilePath = connectionString.Split(new[] { "Data Source=" }, StringSplitOptions.None)[1]
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)[0];
                }
                else
                {
                    return new RestoreResult
                    {
                        Success = false,
                        ErrorMessage = "Could not extract database file path from connection string"
                    };
                }

                // Create a temporary directory for extraction
                var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Extract the backup
                    using (var archive = ZipFile.OpenRead(backupFilePath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            // Sanitize the entry name to prevent path traversal attacks
                            var sanitizedFileName = Path.GetFileName(entry.FullName);
                            if (string.IsNullOrEmpty(sanitizedFileName))
                            {
                                continue;
                            }
                            
                            var extractPath = Path.Combine(tempDir, sanitizedFileName);
                            
                            // Ensure the path is within the temp directory
                            var fullPath = Path.GetFullPath(extractPath);
                            var tempDirFullPath = Path.GetFullPath(tempDir);
                            if (!fullPath.StartsWith(tempDirFullPath))
                            {
                                _logger.LogWarning("Skipping entry with invalid path: {EntryName}", entry.FullName);
                                continue;
                            }
                            
                            entry.ExtractToFile(extractPath, true);
                        }
                    }

                    // Get the extracted database file
                    var extractedDbFiles = Directory.GetFiles(tempDir);
                    if (extractedDbFiles.Length == 0)
                    {
                        return new RestoreResult
                        {
                            Success = false,
                            ErrorMessage = "No database file found in the backup"
                        };
                    }

                    var extractedDbFile = extractedDbFiles[0]; // Take the first file

                    // Disconnect all connections to the database
                    await _dbContext.GetDatabase().CloseConnectionAsync();

                    // Create a backup of the current database
                    var currentDbBackupPath = dbFilePath + ".bak";
                    if (File.Exists(currentDbBackupPath))
                    {
                        File.Delete(currentDbBackupPath);
                    }

                    File.Copy(dbFilePath, currentDbBackupPath);

                    // Replace the database file
                    File.Copy(extractedDbFile, dbFilePath, true);

                    return new RestoreResult
                    {
                        Success = true
                    };
                }
                finally
                {
                    // Clean up temporary directory
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring SQLite database backup");
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = $"An error occurred while restoring SQLite backup: {ex.Message}"
                };
            }
        }

        private static string FormatFileSize(long bytes)
        {
            const int scale = 1024;
            string[] units = { "B", "KB", "MB", "GB", "TB" };

            var unitIndex = 0;
            double size = bytes;

            while (size >= scale && unitIndex < units.Length - 1)
            {
                size /= scale;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        private bool IsValidBackupId(string backupId)
        {
            if (string.IsNullOrWhiteSpace(backupId))
                return false;

            // Backup IDs should only contain alphanumeric characters, underscores, and hyphens
            // This prevents path traversal attacks
            return System.Text.RegularExpressions.Regex.IsMatch(backupId, @"^[a-zA-Z0-9_\-]+$");
        }

        private string EscapeShellArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";

            // For Windows and Unix, wrap in quotes and escape internal quotes
            // This prevents command injection by ensuring special characters are treated as literals
            return $"\"{arg.Replace("\"", "\\\"")}\"";
        }
    }
}
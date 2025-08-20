using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
    /// Service for database backup functionality - PostgreSQL operations
    /// </summary>
    public partial class AdminDatabaseBackupService
    {
        private async Task<BackupResult> CreatePostgresBackupAsync()
        {
            try
            {
                // Get the database connection string
                var connectionString = _dbContext.GetDatabase().GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    return new BackupResult
                    {
                        Success = false,
                        ErrorMessage = "Database connection string is empty"
                    };
                }

                // Parse connection string to get components
                var connectionParams = ParsePostgresConnectionString(connectionString);

                // Create backup file name with timestamp
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var sqlBackupFileName = $"conduit_postgres_backup_{timestamp}.sql";
                var sqlBackupFilePath = Path.Combine(_backupDirectory, sqlBackupFileName);
                var zipBackupFileName = $"conduit_postgres_backup_{timestamp}.zip";
                var zipBackupFilePath = Path.Combine(_backupDirectory, zipBackupFileName);

                // Create dump file using pg_dump
                var pgDumpProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "pg_dump",
                        Arguments = $"-h {EscapeShellArgument(connectionParams["host"])} -p {EscapeShellArgument(connectionParams["port"])} -U {EscapeShellArgument(connectionParams["user"])} -d {EscapeShellArgument(connectionParams["database"])} -F p -f {EscapeShellArgument(sqlBackupFilePath)}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                // Set PGPASSWORD environment variable for authentication
                pgDumpProcess.StartInfo.EnvironmentVariables["PGPASSWORD"] = connectionParams["password"];

                pgDumpProcess.Start();
                var output = await pgDumpProcess.StandardOutput.ReadToEndAsync();
                var error = await pgDumpProcess.StandardError.ReadToEndAsync();
                pgDumpProcess.WaitForExit();

                if (pgDumpProcess.ExitCode != 0)
                {
                    _logger.LogError("pg_dump error: {Error}", error.Replace(Environment.NewLine, ""));
                    return new BackupResult
                    {
                        Success = false,
                        ErrorMessage = $"pg_dump failed with exit code {pgDumpProcess.ExitCode}: {error}"
                    };
                }

                // Create a zip file with the SQL dump
                using (var archive = ZipFile.Open(zipBackupFilePath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry(sqlBackupFileName);

                    using var sqlFileStream = new FileStream(sqlBackupFilePath, FileMode.Open, FileAccess.Read);
                    using var entryStream = entry.Open();

                    await sqlFileStream.CopyToAsync(entryStream);
                }

                // Delete the temporary SQL file
                if (File.Exists(sqlBackupFilePath))
                {
                    File.Delete(sqlBackupFilePath);
                }

                var fileInfo = new FileInfo(zipBackupFilePath);

                var backupInfo = new BackupInfo
                {
                    Id = Path.GetFileNameWithoutExtension(zipBackupFilePath),
                    FileName = zipBackupFileName,
                    CreatedAt = fileInfo.LastWriteTime,
                    SizeBytes = fileInfo.Length,
                    SizeFormatted = FormatFileSize(fileInfo.Length)
                };

                return new BackupResult
                {
                    Success = true,
                    BackupInfo = backupInfo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PostgreSQL database backup");
                return new BackupResult
                {
                    Success = false,
                    ErrorMessage = $"An error occurred while creating PostgreSQL backup: {ex.Message}"
                };
            }
        }

        private async Task<RestoreResult> RestorePostgresBackupAsync(string backupFilePath)
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

                // Parse connection string to get components
                var connectionParams = ParsePostgresConnectionString(connectionString);

                // Create a temporary directory for extraction
                var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Extract the backup
                    string sqlFilePath = string.Empty;

                    using (var archive = ZipFile.OpenRead(backupFilePath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                            {
                                // Sanitize the entry name to prevent path traversal attacks
                                var sanitizedFileName = Path.GetFileName(entry.FullName);
                                if (string.IsNullOrEmpty(sanitizedFileName))
                                {
                                    continue;
                                }
                                
                                sqlFilePath = Path.Combine(tempDir, sanitizedFileName);
                                
                                // Ensure the path is within the temp directory
                                var fullPath = Path.GetFullPath(sqlFilePath);
                                var tempDirFullPath = Path.GetFullPath(tempDir);
                                if (!fullPath.StartsWith(tempDirFullPath))
                                {
                                    _logger.LogWarning("Skipping SQL entry with invalid path: {EntryName}", entry.FullName);
                                    continue;
                                }
                                
                                entry.ExtractToFile(sqlFilePath, true);
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(sqlFilePath) || !File.Exists(sqlFilePath))
                    {
                        return new RestoreResult
                        {
                            Success = false,
                            ErrorMessage = "No SQL file found in the backup"
                        };
                    }

                    // Disconnect all connections to the database
                    await _dbContext.GetDatabase().CloseConnectionAsync();

                    // Drop and recreate the database
                    using (var masterConnection = new Npgsql.NpgsqlConnection(
                        $"Host={connectionParams["host"]};Port={connectionParams["port"]};Username={connectionParams["user"]};Password={connectionParams["password"]};Database=postgres"))
                    {
                        await masterConnection.OpenAsync();

                        // Terminate connections to the database
                        using (var cmd = new Npgsql.NpgsqlCommand(
                            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{connectionParams["database"]}' AND pid <> pg_backend_pid()",
                            masterConnection))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Drop the database
                        using (var cmd = new Npgsql.NpgsqlCommand(
                            $"DROP DATABASE IF EXISTS {connectionParams["database"]}",
                            masterConnection))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Create the database
                        using (var cmd = new Npgsql.NpgsqlCommand(
                            $"CREATE DATABASE {connectionParams["database"]}",
                            masterConnection))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Restore the database using psql
                    var psqlProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "psql",
                            Arguments = $"-h {EscapeShellArgument(connectionParams["host"])} -p {EscapeShellArgument(connectionParams["port"])} -U {EscapeShellArgument(connectionParams["user"])} -d {EscapeShellArgument(connectionParams["database"])} -f {EscapeShellArgument(sqlFilePath)}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    // Set PGPASSWORD environment variable for authentication
                    psqlProcess.StartInfo.EnvironmentVariables["PGPASSWORD"] = connectionParams["password"];

                    psqlProcess.Start();
                    var output = await psqlProcess.StandardOutput.ReadToEndAsync();
                    var error = await psqlProcess.StandardError.ReadToEndAsync();
                    psqlProcess.WaitForExit();

                    if (psqlProcess.ExitCode != 0)
                    {
                        _logger.LogError("psql error: {Error}", error.Replace(Environment.NewLine, ""));
                        return new RestoreResult
                        {
                            Success = false,
                            ErrorMessage = $"psql failed with exit code {psqlProcess.ExitCode}: {error}"
                        };
                    }

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
                _logger.LogError(ex, "Error restoring PostgreSQL database backup");
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = $"An error occurred while restoring PostgreSQL backup: {ex.Message}"
                };
            }
        }

        private static Dictionary<string, string> ParsePostgresConnectionString(string connectionString)
        {
            // Parse different formats of connection strings
            var result = new Dictionary<string, string>();

            if (connectionString.StartsWith("Host=") || connectionString.StartsWith("Server="))
            {
                // Standard key-value format: Host=localhost;Port=5432;...
                var pairs = connectionString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var keyValue = pair.Split(new[] { '=' }, 2);
                    if (keyValue.Length == 2)
                    {
                        var key = keyValue[0].ToLowerInvariant();
                        var value = keyValue[1];

                        // Normalize key names
                        key = key switch
                        {
                            "server" => "host",
                            "userid" => "user",
                            "database" => "database",
                            _ => key
                        };

                        result[key] = value;
                    }
                }
            }
            else if (connectionString.StartsWith("postgresql://"))
            {
                // Connection string URI format: postgresql://user:password@host:port/database
                var uri = new Uri(connectionString);

                result["host"] = uri.Host;
                result["port"] = uri.Port.ToString();
                result["database"] = uri.AbsolutePath.TrimStart('/');

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var userInfo = uri.UserInfo.Split(':');
                    result["user"] = userInfo[0];

                    if (userInfo.Length > 1)
                    {
                        result["password"] = userInfo[1];
                    }
                }
            }

            // Set defaults for missing values
            if (!result.ContainsKey("host")) result["host"] = "localhost";
            if (!result.ContainsKey("port")) result["port"] = "5432";
            if (!result.ContainsKey("user")) result["user"] = "postgres";
            if (!result.ContainsKey("password")) result["password"] = "";
            if (!result.ContainsKey("database")) result["database"] = "postgres";

            return result;
        }
    }
}
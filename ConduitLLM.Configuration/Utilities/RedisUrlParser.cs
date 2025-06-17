using System;
using System.Text.RegularExpressions;

namespace ConduitLLM.Configuration.Utilities
{
    /// <summary>
    /// Utility class for parsing Redis URLs into connection strings
    /// </summary>
    public static class RedisUrlParser
    {
        /// <summary>
        /// Parses a Redis URL into a StackExchange.Redis compatible connection string
        /// </summary>
        /// <param name="redisUrl">Redis URL in format: redis://[username]:[password]@[hostname]:[port]</param>
        /// <returns>StackExchange.Redis compatible connection string</returns>
        public static string ParseRedisUrl(string redisUrl)
        {
            if (string.IsNullOrWhiteSpace(redisUrl))
            {
                throw new ArgumentException("Redis URL cannot be null or empty", nameof(redisUrl));
            }

            // Remove redis:// prefix if present
            if (redisUrl.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
            {
                redisUrl = redisUrl.Substring(8);
            }
            else if (redisUrl.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
            {
                // SSL support - we'll add ssl=true to the connection string
                redisUrl = redisUrl.Substring(9);
                return ParseConnectionString(redisUrl, useSsl: true);
            }

            return ParseConnectionString(redisUrl, useSsl: false);
        }

        private static string ParseConnectionString(string urlPart, bool useSsl)
        {
            // Handle authentication part separately
            string host;
            string port = "6379";
            string? username = null;
            string? password = null;

            // Check if there's an @ sign indicating authentication
            var atIndex = urlPart.LastIndexOf('@');
            if (atIndex >= 0)
            {
                var authPart = urlPart.Substring(0, atIndex);
                var hostPart = urlPart.Substring(atIndex + 1);

                // Parse auth part
                var colonIndex = authPart.IndexOf(':');
                if (colonIndex >= 0)
                {
                    if (colonIndex == 0)
                    {
                        // Format is :password (no username)
                        password = authPart.Substring(1);
                    }
                    else
                    {
                        // Format is username:password
                        username = authPart.Substring(0, colonIndex);
                        password = authPart.Substring(colonIndex + 1);
                    }
                }

                // Parse host:port
                var portIndex = hostPart.LastIndexOf(':');
                if (portIndex >= 0 && portIndex < hostPart.Length - 1)
                {
                    host = hostPart.Substring(0, portIndex);
                    port = hostPart.Substring(portIndex + 1);
                }
                else
                {
                    host = hostPart;
                }
            }
            else
            {
                // No authentication, just host:port
                var portIndex = urlPart.LastIndexOf(':');
                if (portIndex >= 0 && portIndex < urlPart.Length - 1)
                {
                    // Make sure it's actually a port by checking if it's numeric
                    var possiblePort = urlPart.Substring(portIndex + 1);
                    if (int.TryParse(possiblePort, out _))
                    {
                        host = urlPart.Substring(0, portIndex);
                        port = possiblePort;
                    }
                    else
                    {
                        host = urlPart;
                    }
                }
                else
                {
                    host = urlPart;
                }
            }

            // Build StackExchange.Redis connection string
            var connectionString = $"{host}:{port}";

            if (!string.IsNullOrEmpty(password))
            {
                connectionString += $",password={password}";
            }

            if (!string.IsNullOrEmpty(username))
            {
                connectionString += $",user={username}";
            }

            if (useSsl)
            {
                connectionString += ",ssl=true";
            }

            // Add some reasonable defaults for production use
            connectionString += ",abortConnect=false,connectTimeout=10000,syncTimeout=10000";

            return connectionString;
        }
    }
}
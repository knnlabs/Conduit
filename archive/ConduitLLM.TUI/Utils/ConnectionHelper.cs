using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.TUI.Utils;

/// <summary>
/// Provides utilities for connection handling and diagnostics.
/// </summary>
public static class ConnectionHelper
{
    private static readonly string[] LocalHostNames = 
    {
        "localhost",
        "127.0.0.1",
        "::1",
        "0.0.0.0"
    };

    /// <summary>
    /// Determines if the provided hostname or URL represents a local machine address.
    /// </summary>
    /// <param name="hostOrUrl">The hostname, IP address, or full URL to check.</param>
    /// <returns>True if the host is identified as local, false otherwise.</returns>
    public static bool IsLocalHost(string hostOrUrl)
    {
        if (string.IsNullOrWhiteSpace(hostOrUrl))
            return false;

        // Extract hostname from URL if a full URL is provided
        string hostname = ExtractHostname(hostOrUrl);
        
        // Check common localhost identifiers
        if (LocalHostNames.Any(local => string.Equals(hostname, local, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Check if it's the machine's actual hostname
        try
        {
            var localHostname = Environment.MachineName;
            if (string.Equals(hostname, localHostname, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check if it resolves to a loopback address
            var hostEntry = Dns.GetHostEntry(hostname);
            return hostEntry.AddressList.Any(addr => IPAddress.IsLoopback(addr));
        }
        catch
        {
            // If DNS resolution fails, assume it's not local
            return false;
        }
    }

    /// <summary>
    /// Extracts the hostname from a URL or returns the input if it's already a hostname.
    /// </summary>
    /// <param name="hostOrUrl">The hostname or URL to extract from.</param>
    /// <returns>The extracted hostname.</returns>
    private static string ExtractHostname(string hostOrUrl)
    {
        try
        {
            if (Uri.TryCreate(hostOrUrl, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }
        catch
        {
            // Fall through to return the original string
        }

        return hostOrUrl;
    }

    /// <summary>
    /// Generates helpful connection troubleshooting suggestions based on the target host and error.
    /// </summary>
    /// <param name="hostOrUrl">The hostname or URL that failed to connect.</param>
    /// <param name="error">The exception that occurred during connection.</param>
    /// <returns>A formatted message with troubleshooting suggestions.</returns>
    public static string GetConnectionTroubleshootingMessage(string hostOrUrl, Exception error)
    {
        var hostname = ExtractHostname(hostOrUrl);
        var isLocal = IsLocalHost(hostname);
        var port = ExtractPort(hostOrUrl);

        var message = $"Unable to connect to {hostOrUrl}\n";
        message += $"Error: {error.Message}\n\n";

        if (isLocal)
        {
            message += "This appears to be a local service. Here are some troubleshooting steps:\n\n";
            message += "🔍 Check if the service is running:\n";
            
            if (port.HasValue)
            {
                message += $"   • Run: netstat -tulpn | grep :{port} (Linux/macOS)\n";
                message += $"   • Run: netstat -an | findstr :{port} (Windows)\n\n";
            }
            
            message += "🐳 If using Docker:\n";
            message += "   • Run: docker ps\n";
            message += "   • Ensure containers are running and healthy\n";
            message += "   • Check: docker-compose ps\n";
            message += "   • Restart services: docker-compose restart\n";
            message += "   • View logs: docker-compose logs [service-name]\n\n";
            
            message += "🌐 Port and network checks:\n";
            if (port.HasValue)
            {
                message += $"   • Verify the service is listening on port {port}\n";
                message += $"   • Check if port {port} is blocked by firewall\n";
            }
            message += "   • Ensure no other service is using the port\n";
            message += "   • Try connecting from another terminal: curl " + hostOrUrl + "\n\n";
            
            message += "📋 Configuration checks:\n";
            message += "   • Verify environment variables (API URLs, ports)\n";
            message += "   • Check appsettings.json configuration\n";
            message += "   • Ensure Docker port mappings are correct\n";
        }
        else
        {
            message += "This appears to be a remote service. Here are some troubleshooting steps:\n\n";
            message += "🌐 Network connectivity:\n";
            message += $"   • Run: ping {hostname}\n";
            message += $"   • Check: nslookup {hostname}\n";
            if (port.HasValue)
            {
                message += $"   • Test port: telnet {hostname} {port}\n";
            }
            message += "\n🔧 Service checks:\n";
            message += "   • Verify the remote service is running\n";
            message += "   • Check firewall/security group settings\n";
            message += "   • Ensure correct URL and port configuration\n";
            message += "   • Verify SSL/TLS certificate if using HTTPS\n";
        }

        return message;
    }

    /// <summary>
    /// Extracts the port number from a URL or hostname:port string.
    /// </summary>
    /// <param name="hostOrUrl">The hostname or URL to extract port from.</param>
    /// <returns>The port number if found, null otherwise.</returns>
    private static int? ExtractPort(string hostOrUrl)
    {
        try
        {
            if (Uri.TryCreate(hostOrUrl, UriKind.Absolute, out var uri))
            {
                return uri.Port != -1 ? uri.Port : null;
            }

            // Try to parse hostname:port format
            var lastColonIndex = hostOrUrl.LastIndexOf(':');
            if (lastColonIndex > 0 && lastColonIndex < hostOrUrl.Length - 1)
            {
                var portString = hostOrUrl.Substring(lastColonIndex + 1);
                if (int.TryParse(portString, out var port))
                {
                    return port;
                }
            }
        }
        catch
        {
            // Fall through to return null
        }

        return null;
    }

    /// <summary>
    /// Creates a user-friendly error message for connection failures.
    /// </summary>
    /// <param name="serviceName">The name of the service (e.g., "Admin API", "Core API").</param>
    /// <param name="hostOrUrl">The URL or hostname that failed.</param>
    /// <param name="error">The exception that occurred.</param>
    /// <returns>A formatted error message with troubleshooting information.</returns>
    public static string FormatConnectionError(string serviceName, string hostOrUrl, Exception error)
    {
        var message = $"❌ Connection Failed\n\n";
        message += $"Unable to connect to {serviceName} at {hostOrUrl}\n\n";
        message += GetConnectionTroubleshootingMessage(hostOrUrl, error);
        return message;
    }

    /// <summary>
    /// Attempts to ping a host to check basic connectivity.
    /// </summary>
    /// <param name="hostname">The hostname to ping.</param>
    /// <param name="timeout">Timeout in milliseconds (default: 5000).</param>
    /// <returns>True if ping succeeds, false otherwise.</returns>
    public static async Task<bool> CanPingHostAsync(string hostname, int timeout = 5000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ExtractHostname(hostname), timeout);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}
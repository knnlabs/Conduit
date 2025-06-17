using System.Net;
using System.Net.Sockets;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for classifying IP addresses based on their type and range
/// </summary>
public interface IIpAddressClassifier
{
    /// <summary>
    /// Determines if an IP address is a private or intranet address
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>True if the address is private or intranet, false otherwise</returns>
    bool IsPrivateOrIntranet(string ipAddress);

    /// <summary>
    /// Determines if an IP address is a loopback address
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>True if the address is a loopback address, false otherwise</returns>
    bool IsLoopback(string ipAddress);

    /// <summary>
    /// Determines if an IP address is a link-local address
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>True if the address is a link-local address, false otherwise</returns>
    bool IsLinkLocal(string ipAddress);

    /// <summary>
    /// Gets the classification type of an IP address
    /// </summary>
    /// <param name="ipAddress">The IP address to classify</param>
    /// <returns>The classification type</returns>
    IpClassification GetClassification(string ipAddress);
}

/// <summary>
/// Types of IP address classifications
/// </summary>
public enum IpClassification
{
    /// <summary>
    /// Public internet-routable address
    /// </summary>
    Public,

    /// <summary>
    /// Private network address (RFC 1918)
    /// </summary>
    Private,

    /// <summary>
    /// Loopback address
    /// </summary>
    Loopback,

    /// <summary>
    /// Link-local address
    /// </summary>
    LinkLocal,

    /// <summary>
    /// Invalid or unrecognized address
    /// </summary>
    Invalid
}

/// <summary>
/// Implementation of IP address classifier
/// </summary>
public class IpAddressClassifier : IIpAddressClassifier
{
    // Private IPv4 ranges (RFC 1918)
    private static readonly (string Cidr, string Name)[] PrivateIpv4Ranges = new[]
    {
        ("10.0.0.0/8", "Class A Private"),
        ("172.16.0.0/12", "Class B Private"),
        ("192.168.0.0/16", "Class C Private")
    };

    // Loopback ranges
    private static readonly (string Cidr, string Name)[] LoopbackRanges = new[]
    {
        ("127.0.0.0/8", "IPv4 Loopback"),
        ("::1/128", "IPv6 Loopback")
    };

    // Link-local ranges
    private static readonly (string Cidr, string Name)[] LinkLocalRanges = new[]
    {
        ("169.254.0.0/16", "IPv4 Link-Local"),
        ("fe80::/10", "IPv6 Link-Local")
    };

    // Private IPv6 ranges
    private static readonly (string Cidr, string Name)[] PrivateIpv6Ranges = new[]
    {
        ("fc00::/7", "IPv6 Unique Local"),
        ("fd00::/8", "IPv6 Unique Local (Locally Assigned)")
    };

    /// <inheritdoc />
    public bool IsPrivateOrIntranet(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        // Check loopback first (considered intranet)
        if (IsLoopback(ipAddress))
            return true;

        // Check private ranges based on IP version
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return PrivateIpv4Ranges.Any(range => IpAddressValidator.IsIpInCidrRange(ipAddress, range.Cidr));
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return PrivateIpv6Ranges.Any(range => IpAddressValidator.IsIpInCidrRange(ipAddress, range.Cidr));
        }

        return false;
    }

    /// <inheritdoc />
    public bool IsLoopback(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        // Use built-in IsLoopback for simple cases
        if (IPAddress.IsLoopback(ip))
            return true;

        // Additional check for full loopback range
        return LoopbackRanges.Any(range => IpAddressValidator.IsIpInCidrRange(ipAddress, range.Cidr));
    }

    /// <inheritdoc />
    public bool IsLinkLocal(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        // Check if it's in link-local ranges
        return LinkLocalRanges.Any(range => IpAddressValidator.IsIpInCidrRange(ipAddress, range.Cidr));
    }

    /// <inheritdoc />
    public IpClassification GetClassification(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var ip))
            return IpClassification.Invalid;

        if (IsLoopback(ipAddress))
            return IpClassification.Loopback;

        if (IsLinkLocal(ipAddress))
            return IpClassification.LinkLocal;

        if (IsPrivateOrIntranet(ipAddress))
            return IpClassification.Private;

        return IpClassification.Public;
    }
}
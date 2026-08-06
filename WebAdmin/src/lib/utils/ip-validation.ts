/**
 * Shared IP address / CIDR validation predicates.
 *
 * Single source of truth for IP validation across the WebAdmin — the Admin API
 * accepts both IPv4 and IPv6 addresses (with CIDR prefixes /0-32 for IPv4 and
 * /0-128 for IPv6), so UI validation must accept the same set.
 */

export function isValidIpv4(ip: string): boolean {
  const parts = ip.split('.');
  if (parts.length !== 4) return false;
  return parts.every((part) => {
    const num = parseInt(part, 10);
    return !isNaN(num) && num >= 0 && num <= 255 && part === num.toString();
  });
}

export function isValidIpv6(ip: string): boolean {
  if (ip.includes('::ffff:')) return isValidIpv4(ip.split('::ffff:')[1]);
  const ipv6Regex = /^(([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|::)$/;
  return ipv6Regex.test(ip);
}

/** True when the value is a valid plain IPv4 or IPv6 address (no CIDR prefix). */
export function isValidIp(ip: string): boolean {
  return isValidIpv4(ip) || isValidIpv6(ip);
}

/**
 * Validates a plain IPv4/IPv6 address.
 * @returns an error message, or null when valid.
 */
export function getIpValidationError(value: string): string | null {
  if (!isValidIp(value)) {
    return 'Invalid IP address format (IPv4 and IPv6 are supported, e.g., 192.168.1.1 or 2001:db8::1)';
  }
  return null;
}

/**
 * Validates an IPv4/IPv6 address or CIDR range (/0-32 for IPv4, /0-128 for IPv6).
 * @returns an error message, or null when valid.
 */
export function getIpOrCidrValidationError(value: string): string | null {
  if (!value.includes('/')) {
    if (!isValidIp(value)) {
      return 'Invalid IP address format (IPv4 and IPv6 are supported, e.g., 192.168.1.1 or 2001:db8::1)';
    }
    return null;
  }
  const [ip, prefixStr] = value.split('/');
  const prefix = parseInt(prefixStr, 10);
  if (!isValidIp(ip)) {
    return 'Invalid IP address format in CIDR notation (e.g., 192.168.1.0/24 or 2001:db8::/32)';
  }
  if (isValidIpv4(ip) && (isNaN(prefix) || prefix < 0 || prefix > 32)) {
    return 'IPv4 CIDR prefix must be between 0 and 32';
  }
  if (!isValidIpv4(ip) && (isNaN(prefix) || prefix < 0 || prefix > 128)) {
    return 'IPv6 CIDR prefix must be between 0 and 128';
  }
  return null;
}

/**
 * Common type guard and validation utilities
 */

/**
 * Check that a value is a non-empty string (after trimming).
 */
export function isNonEmptyString(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0;
}

/**
 * Check that a value is a positive number (> 0).
 */
export function isPositiveNumber(value: unknown): value is number {
  return typeof value === "number" && !isNaN(value) && value > 0;
}

/**
 * Check that a value looks like a valid email address.
 */
export function isValidEmail(value: unknown): value is string {
  if (!isNonEmptyString(value)) return false;
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(value);
}

/**
 * Check that a value is a valid URL.
 */
export function isValidUrl(value: unknown): value is string {
  if (!isNonEmptyString(value)) return false;
  try {
    new URL(value);
    return true;
  } catch {
    return false;
  }
}

/**
 * Check that a value is one of a set of allowed enum strings.
 */
export function isValidEnumValue<T extends string>(
  value: unknown,
  enumValues: readonly T[],
): value is T {
  return typeof value === "string" && enumValues.includes(value as T);
}

/**
 * Check that a value is a valid IPv4 address.
 */
export function isValidIPv4(value: string): boolean {
  const ipRegex =
    /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$/;
  return ipRegex.test(value);
}

/**
 * Check that a value is a valid CIDR notation (IPv4).
 */
export function isValidCIDR(value: string): boolean {
  const cidrRegex =
    /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\/(?:3[0-2]|[12]?[0-9])$/;
  return cidrRegex.test(value);
}

/**
 * Check that a value is a valid IPv4 address or CIDR notation.
 */
export function isValidIPOrCIDR(value: string): boolean {
  return isValidIPv4(value) || isValidCIDR(value);
}

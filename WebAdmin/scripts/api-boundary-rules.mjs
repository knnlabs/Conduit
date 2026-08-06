const genericModelTransportPattern = /\bclient\s*\[\s*['"](?:get|post|put|delete)['"]\s*\]/;

export function usesGenericModelTransport(contents) {
  return genericModelTransportPattern.test(contents);
}

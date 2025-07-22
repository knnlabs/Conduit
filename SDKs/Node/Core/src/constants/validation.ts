/**
 * Chat message role constants.
 */
export const CHAT_ROLES = {
  SYSTEM: 'system',
  USER: 'user',
  ASSISTANT: 'assistant',
  TOOL: 'tool',
} as const;

export type ChatRole = typeof CHAT_ROLES[keyof typeof CHAT_ROLES];

/**
 * Chat role validation helpers.
 */
export const ChatRoleHelpers = {
  /**
   * Get all valid chat roles.
   */
  getAllRoles: (): readonly ChatRole[] => Object.values(CHAT_ROLES),

  /**
   * Check if a role is valid.
   */
  isValidRole: (role: string): role is ChatRole => 
    Object.values(CHAT_ROLES).includes(role as ChatRole),

  /**
   * Check if a role requires a tool call ID.
   */
  requiresToolCallId: (role: string): boolean =>
    role === CHAT_ROLES.TOOL,

  /**
   * Validate a role and throw an error if invalid.
   */
  validateRole: (role: string): asserts role is ChatRole => {
    if (!ChatRoleHelpers.isValidRole(role)) {
      throw new Error(`Invalid chat role: ${role}. Valid roles are: ${Object.values(CHAT_ROLES).join(', ')}`);
    }
  },
} as const;

/**
 * Image response format constants.
 */
export const IMAGE_RESPONSE_FORMATS = {
  URL: 'url',
  BASE64_JSON: 'b64_json',
} as const;

export type ImageResponseFormat = typeof IMAGE_RESPONSE_FORMATS[keyof typeof IMAGE_RESPONSE_FORMATS];

/**
 * Image quality constants.
 */
export const IMAGE_QUALITY = {
  STANDARD: 'standard',
  HD: 'hd',
} as const;

export type ImageQuality = typeof IMAGE_QUALITY[keyof typeof IMAGE_QUALITY];

/**
 * Image style constants.
 */
export const IMAGE_STYLE = {
  VIVID: 'vivid',
  NATURAL: 'natural',
} as const;

export type ImageStyle = typeof IMAGE_STYLE[keyof typeof IMAGE_STYLE];

/**
 * Image size constants.
 */
export const IMAGE_SIZES = {
  SMALL: '256x256',
  MEDIUM: '512x512',
  LARGE: '1024x1024',
  WIDE: '1792x1024',
  TALL: '1024x1792',
} as const;

export type ImageSize = typeof IMAGE_SIZES[keyof typeof IMAGE_SIZES];

/**
 * Image validation helpers.
 */
export const ImageValidationHelpers = {
  /**
   * Check if response format is valid.
   */
  isValidResponseFormat: (format: string): format is ImageResponseFormat =>
    Object.values(IMAGE_RESPONSE_FORMATS).includes(format as ImageResponseFormat),

  /**
   * Check if quality is valid.
   */
  isValidQuality: (quality: string): quality is ImageQuality =>
    Object.values(IMAGE_QUALITY).includes(quality as ImageQuality),

  /**
   * Check if style is valid.
   */
  isValidStyle: (style: string): style is ImageStyle =>
    Object.values(IMAGE_STYLE).includes(style as ImageStyle),

  /**
   * Check if size is valid.
   */
  isValidSize: (size: string): size is ImageSize =>
    Object.values(IMAGE_SIZES).includes(size as ImageSize),

  /**
   * Get all valid response formats.
   */
  getAllResponseFormats: (): readonly ImageResponseFormat[] => Object.values(IMAGE_RESPONSE_FORMATS),

  /**
   * Get all valid qualities.
   */
  getAllQualities: (): readonly ImageQuality[] => Object.values(IMAGE_QUALITY),

  /**
   * Get all valid styles.
   */
  getAllStyles: (): readonly ImageStyle[] => Object.values(IMAGE_STYLE),

  /**
   * Get all valid sizes.
   */
  getAllSizes: (): readonly ImageSize[] => Object.values(IMAGE_SIZES),
} as const;
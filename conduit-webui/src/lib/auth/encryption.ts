/**
 * Client-side encryption utilities for sensitive data storage
 * Uses Web Crypto API for secure encryption/decryption
 */

class EncryptionService {
  private static ALGORITHM = 'AES-GCM';
  private static KEY_LENGTH = 256;
  private static IV_LENGTH = 12;
  
  // Derive encryption key from browser session info and a static salt
  private static async deriveKey(): Promise<CryptoKey> {
    const keyMaterial = await crypto.subtle.importKey(
      'raw',
      new TextEncoder().encode(this.getBrowserFingerprint()),
      'PBKDF2',
      false,
      ['deriveBits', 'deriveKey']
    );

    return crypto.subtle.deriveKey(
      {
        name: 'PBKDF2',
        salt: new TextEncoder().encode('conduit-webui-salt-v1'),
        iterations: 100000,
        hash: 'SHA-256',
      },
      keyMaterial,
      { name: this.ALGORITHM, length: this.KEY_LENGTH },
      false,
      ['encrypt', 'decrypt']
    );
  }

  // Create a browser fingerprint for key derivation
  private static getBrowserFingerprint(): string {
    // Use stable browser characteristics for key derivation
    const userAgent = navigator.userAgent;
    const language = navigator.language;
    const platform = navigator.platform;
    const screenResolution = `${screen.width}x${screen.height}`;
    const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    
    return `${userAgent}-${language}-${platform}-${screenResolution}-${timezone}`;
  }

  /**
   * Encrypts a string using AES-GCM with a browser-derived key
   */
  static async encrypt(plaintext: string): Promise<string> {
    try {
      if (!crypto.subtle) {
        throw new Error('Web Crypto API not available');
      }

      const key = await this.deriveKey();
      const iv = crypto.getRandomValues(new Uint8Array(this.IV_LENGTH));
      const encoder = new TextEncoder();
      const data = encoder.encode(plaintext);

      const encrypted = await crypto.subtle.encrypt(
        {
          name: this.ALGORITHM,
          iv: iv,
        },
        key,
        data
      );

      // Combine IV and encrypted data
      const combined = new Uint8Array(iv.length + encrypted.byteLength);
      combined.set(iv);
      combined.set(new Uint8Array(encrypted), iv.length);

      // Convert to base64 for storage
      return btoa(String.fromCharCode(...combined));
    } catch (error) {
      console.error('Encryption failed:', error);
      throw new Error('Failed to encrypt data');
    }
  }

  /**
   * Decrypts a string encrypted with the encrypt method
   */
  static async decrypt(encryptedData: string): Promise<string> {
    try {
      if (!crypto.subtle) {
        throw new Error('Web Crypto API not available');
      }

      // Convert from base64
      const combined = Uint8Array.from(atob(encryptedData), c => c.charCodeAt(0));
      
      // Extract IV and encrypted data
      const iv = combined.slice(0, this.IV_LENGTH);
      const encrypted = combined.slice(this.IV_LENGTH);

      const key = await this.deriveKey();

      const decrypted = await crypto.subtle.decrypt(
        {
          name: this.ALGORITHM,
          iv: iv,
        },
        key,
        encrypted
      );

      const decoder = new TextDecoder();
      return decoder.decode(decrypted);
    } catch (error) {
      console.error('Decryption failed:', error);
      throw new Error('Failed to decrypt data');
    }
  }

  /**
   * Check if encryption is available in the current environment
   */
  static isAvailable(): boolean {
    return typeof crypto !== 'undefined' && 
           typeof crypto.subtle !== 'undefined' &&
           typeof crypto.getRandomValues !== 'undefined';
  }
}

export { EncryptionService };
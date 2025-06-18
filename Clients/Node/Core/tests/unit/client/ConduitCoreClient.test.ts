import { ConduitCoreClient } from '../../../src/client/ConduitCoreClient';
import { ChatService } from '../../../src/services/ChatService';
import { ModelsService } from '../../../src/services/ModelsService';

describe('ConduitCoreClient', () => {
  const mockConfig = {
    apiKey: 'test-api-key',
    baseURL: 'https://api.test.com',
  };

  describe('constructor', () => {
    it('should create client with provided config', () => {
      const client = new ConduitCoreClient(mockConfig);
      
      expect(client).toBeInstanceOf(ConduitCoreClient);
      expect(client.chat.completions).toBeInstanceOf(ChatService);
      expect(client.models).toBeInstanceOf(ModelsService);
    });

    it('should use default baseURL when not provided', () => {
      const client = new ConduitCoreClient({
        apiKey: 'test-key',
      });
      
      expect(client).toBeInstanceOf(ConduitCoreClient);
    });
  });

  describe('fromApiKey', () => {
    it('should create client from API key', () => {
      const client = ConduitCoreClient.fromApiKey('test-key');
      
      expect(client).toBeInstanceOf(ConduitCoreClient);
    });

    it('should accept custom baseURL', () => {
      const client = ConduitCoreClient.fromApiKey('test-key', 'https://custom.api.com');
      
      expect(client).toBeInstanceOf(ConduitCoreClient);
    });
  });
});
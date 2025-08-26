import {
  ConversationExporter,
  ConversationImporter,
  type ExportableMessage,
  type ExportTemplate
} from '../conversation-export';

describe('ConversationExporter', () => {
  // Test data
  const sampleMessages: ExportableMessage[] = [
    {
      id: 'msg1',
      role: 'user',
      content: 'Hello, how are you?',
      timestamp: new Date('2024-01-01T10:00:00Z'),
      model: 'gpt-4'
    },
    {
      id: 'msg2',
      role: 'assistant',
      content: 'I am doing well, thank you! How can I help you today?',
      timestamp: new Date('2024-01-01T10:00:05Z'),
      model: 'gpt-4',
      metadata: {
        tokensUsed: 15,
        latency: 1200,
        finishReason: 'stop'
      }
    },
    {
      id: 'msg3',
      role: 'user',
      content: 'Can you help me write some code?',
      timestamp: new Date('2024-01-01T10:01:00Z'),
      images: [
        {
          url: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==',
          width: 100,
          height: 100,
          detail: 'auto'
        }
      ]
    },
    {
      id: 'msg4',
      role: 'system',
      content: 'You are a helpful programming assistant.',
      timestamp: new Date('2024-01-01T09:59:00Z')
    }
  ];

  describe('toJSON', () => {
    it('should export basic conversation to JSON', () => {
      const json = ConversationExporter.toJSON(sampleMessages.slice(0, 2));
      const parsed = JSON.parse(json);

      expect(parsed.version).toBe('1.0');
      expect(parsed.totalMessages).toBe(2);
      expect(parsed.messages).toHaveLength(2);
      expect(parsed.messages[0].id).toBe('msg1');
      expect(parsed.messages[0].role).toBe('user');
      expect(parsed.messages[0].content).toBe('Hello, how are you?');
    });

    it('should include metadata when requested', () => {
      const json = ConversationExporter.toJSON(sampleMessages, { 
        includeMetadata: true 
      });
      const parsed = JSON.parse(json);

      expect(parsed.messages[1].metadata).toEqual({
        tokensUsed: 15,
        latency: 1200,
        finishReason: 'stop'
      });
    });

    it('should exclude metadata when not requested', () => {
      const json = ConversationExporter.toJSON(sampleMessages, { 
        includeMetadata: false 
      });
      const parsed = JSON.parse(json);

      expect(parsed.messages[1].metadata).toBeUndefined();
    });

    it('should include timestamps by default', () => {
      const json = ConversationExporter.toJSON(sampleMessages.slice(0, 1));
      const parsed = JSON.parse(json);

      expect(parsed.messages[0].timestamp).toBe('2024-01-01T10:00:00.000Z');
    });

    it('should exclude timestamps when requested', () => {
      const json = ConversationExporter.toJSON(sampleMessages.slice(0, 1), {
        includeTimestamps: false
      });
      const parsed = JSON.parse(json);

      expect(parsed.messages[0].timestamp).toBeUndefined();
    });

    it('should filter out system messages when requested', () => {
      const json = ConversationExporter.toJSON(sampleMessages, {
        includeSystemMessages: false
      });
      const parsed = JSON.parse(json);

      expect(parsed.messages).toHaveLength(3);
      expect(parsed.messages.every((msg: ExportableMessage) => msg.role !== 'system')).toBe(true);
    });

    it('should include system messages by default', () => {
      const json = ConversationExporter.toJSON(sampleMessages);
      const parsed = JSON.parse(json);

      expect(parsed.messages).toHaveLength(4);
      expect(parsed.messages.some((msg: ExportableMessage) => msg.role === 'system')).toBe(true);
    });

    it('should include images in export', () => {
      const json = ConversationExporter.toJSON(sampleMessages);
      const parsed = JSON.parse(json);

      const messageWithImage = parsed.messages.find((msg: ExportableMessage) => msg.images);
      expect(messageWithImage).toBeDefined();
      expect(messageWithImage.images).toHaveLength(1);
      expect(messageWithImage.images[0].width).toBe(100);
      expect(messageWithImage.images[0].height).toBe(100);
    });

    it('should calculate conversation statistics', () => {
      const json = ConversationExporter.toJSON(sampleMessages);
      const parsed = JSON.parse(json);

      expect(parsed.totalMessages).toBe(4);
      expect(parsed.model).toBe('gpt-4');
      expect(parsed.totalTokens).toBe(15);
      expect(parsed.participants).toEqual(['assistant', 'system', 'user']);
    });

    it('should handle empty conversation', () => {
      const json = ConversationExporter.toJSON([]);
      const parsed = JSON.parse(json);

      expect(parsed.totalMessages).toBe(0);
      expect(parsed.messages).toHaveLength(0);
      expect(parsed.model).toBeUndefined();
    });
  });

  describe('toMarkdown', () => {
    it('should export basic conversation to Markdown', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages.slice(0, 2));

      expect(markdown).toContain('# Conversation Export');
      expect(markdown).toContain('## Messages');
      expect(markdown).toContain('### User');
      expect(markdown).toContain('### Assistant');
      expect(markdown).toContain('Hello, how are you?');
      expect(markdown).toContain('I am doing well, thank you!');
    });

    it('should include table of contents when requested', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages.slice(0, 2), {
        includeTableOfContents: true
      });

      expect(markdown).toContain('## Table of Contents');
      expect(markdown).toContain('1. [user: Hello, how are you?](#message-1)');
      expect(markdown).toContain('2. [assistant: I am doing well, thank you! How can I help you tod...');
    });

    it('should use custom header levels', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages.slice(0, 1), {
        headerLevel: 2
      });

      expect(markdown).toContain('## User');
    });

    it('should include message IDs when requested', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages.slice(0, 1), {
        includeMessageIds: true
      });

      expect(markdown).toContain('### User (msg1)');
    });

    it('should include timestamps by default', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages.slice(0, 1));

      expect(markdown).toContain('2024-01-01T10:00:00.000Z');
    });

    it('should include conversation statistics', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages);

      expect(markdown).toContain('**Primary Model:** gpt-4');
      expect(markdown).toContain('**Total Messages:** 4');
      expect(markdown).toContain('**Estimated Tokens:** 15');
    });

    it('should include image information', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages);

      expect(markdown).toContain('**Images:**');
      expect(markdown).toContain('- Image 1 (100x100)');
    });

    it('should include metadata when requested', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages, {
        includeMetadata: true
      });

      expect(markdown).toContain('**Metadata:**');
      expect(markdown).toContain('- Tokens: 15');
      expect(markdown).toContain('- Latency: 1200ms');
    });

    it('should exclude system messages when requested', () => {
      const markdown = ConversationExporter.toMarkdown(sampleMessages, {
        includeSystemMessages: false
      });

      expect(markdown).not.toContain('### System');
      expect(markdown).not.toContain('You are a helpful programming assistant.');
    });
  });

  describe('toCSV', () => {
    it('should export basic conversation to CSV', () => {
      const csv = ConversationExporter.toCSV(sampleMessages.slice(0, 2));
      const lines = csv.split('\n').filter(line => line.trim());

      expect(lines[0]).toBe('id,role,content,timestamp');
      expect(lines[1]).toBe('msg1,user,"Hello, how are you?",2024-01-01T10:00:00.000Z');
      expect(lines[2]).toContain('msg2,assistant');
      expect(lines[2]).toContain('I am doing well, thank you!');
    });

    it('should use custom columns', () => {
      const csv = ConversationExporter.toCSV(sampleMessages.slice(0, 2), {
        columns: ['id', 'role', 'tokensUsed']
      });
      const lines = csv.split('\n').filter(line => line.trim());

      expect(lines[0]).toBe('id,role,tokensUsed');
      expect(lines[1]).toBe('msg1,user,');
      expect(lines[2]).toBe('msg2,assistant,15');
    });

    it('should use custom delimiter', () => {
      const csv = ConversationExporter.toCSV(sampleMessages.slice(0, 1), {
        delimiter: ';'
      });

      expect(csv).toContain('id;role;content;timestamp');
      expect(csv).toContain('msg1;user;"Hello, how are you?";');
    });

    it('should handle text with commas', () => {
      const messagesWithCommas: ExportableMessage[] = [{
        id: 'msg1',
        role: 'user',
        content: 'Hello, world, how are you?',
        timestamp: new Date('2024-01-01T10:00:00Z')
      }];

      const csv = ConversationExporter.toCSV(messagesWithCommas);

      expect(csv).toContain('"Hello, world, how are you?"');
    });

    it('should handle text with newlines', () => {
      const messagesWithNewlines: ExportableMessage[] = [{
        id: 'msg1',
        role: 'user',
        content: 'Line 1\nLine 2',
        timestamp: new Date('2024-01-01T10:00:00Z')
      }];

      const csv = ConversationExporter.toCSV(messagesWithNewlines);

      expect(csv).toContain('Line 1 Line 2'); // Newlines should be converted to spaces
    });

    it('should exclude headers when requested', () => {
      const csv = ConversationExporter.toCSV(sampleMessages.slice(0, 1), {
        includeHeaders: false
      });

      expect(csv).not.toContain('id,role,content,timestamp');
      expect(csv).toContain('msg1,user,"Hello, how are you?",');
    });

    it('should handle image fields', () => {
      const csv = ConversationExporter.toCSV(sampleMessages, {
        columns: ['id', 'hasImages', 'imageCount']
      });
      const lines = csv.split('\n').filter(line => line.trim());

      expect(lines[0]).toBe('id,hasImages,imageCount');
      expect(lines[3]).toBe('msg3,true,1'); // Message with image
      expect(lines[1]).toBe('msg1,false,'); // Message without image
    });
  });

  describe('toCustomFormat', () => {
    it('should export using custom template', () => {
      const template: ExportTemplate = {
        header: 'Chat Log - {{messages.length}} messages',
        messageTemplate: '[{{message.timestamp}}] {{message.role}}: {{message.content}}',
        messageSeparator: '\n',
        footer: 'End of conversation'
      };

      const output = ConversationExporter.toCustomFormat(sampleMessages.slice(0, 2), template);

      expect(output).toContain('Chat Log - 2');
      expect(output).toContain('user: Hello, how are you?');
      expect(output).toContain('assistant: I am doing well, thank you!');
      expect(output).toContain('End of conversation');
    });

    it('should handle template variables', () => {
      const template: ExportTemplate = {
        messageTemplate: '{{message.role}}: {{message.content}} ({{customVar}})',
        variables: {
          customVar: 'Custom Value'
        }
      };

      const output = ConversationExporter.toCustomFormat(sampleMessages.slice(0, 1), template);

      expect(output).toContain('Hello, how are you? (Custom Value)');
    });

    it('should handle missing template variables', () => {
      const template: ExportTemplate = {
        messageTemplate: '{{message.role}}: {{missingVar}}'
      };

      const output = ConversationExporter.toCustomFormat(sampleMessages.slice(0, 1), template);

      expect(output).toContain('{{missingVar}}'); // Should leave placeholder
    });
  });
});

describe('ConversationImporter', () => {
  const validConversationJSON = {
    version: '1.0',
    exported: '2024-01-01T12:00:00Z',
    totalMessages: 2,
    messages: [
      {
        id: 'msg1',
        role: 'user',
        content: 'Hello',
        timestamp: '2024-01-01T10:00:00Z'
      },
      {
        id: 'msg2',
        role: 'assistant',
        content: 'Hi there!',
        timestamp: '2024-01-01T10:00:05Z',
        metadata: {
          tokensUsed: 5,
          latency: 800
        }
      }
    ]
  };

  describe('fromJSON', () => {
    it('should import valid JSON conversation', () => {
      const json = JSON.stringify(validConversationJSON);
      const result = ConversationImporter.fromJSON(json);

      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data).toHaveLength(2);
        expect(result.data[0].id).toBe('msg1');
        expect(result.data[0].role).toBe('user');
        expect(result.data[0].content).toBe('Hello');
        expect(result.data[0].timestamp).toBeInstanceOf(Date);
      }
    });

    it('should handle invalid JSON', () => {
      const result = ConversationImporter.fromJSON('invalid json');

      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.errors).toHaveLength(1);
        expect(result.errors[0].code).toBe('INVALID_JSON');
      }
    });

    it('should validate imported messages', () => {
      const invalidJSON = JSON.stringify({
        messages: [
          {
            id: 'msg1',
            role: 'invalid_role',
            content: 'Hello'
          }
        ]
      });

      const result = ConversationImporter.fromJSON(invalidJSON);

      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.errors.some(e => e.code === 'INVALID_ROLE')).toBe(true);
      }
    });

    it('should apply message limit', () => {
      const manyMessages = {
        messages: Array.from({ length: 100 }, (_, i) => ({
          id: `msg${i}`,
          role: 'user',
          content: `Message ${i}`,
          timestamp: new Date().toISOString()
        }))
      };

      const result = ConversationImporter.fromJSON(
        JSON.stringify(manyMessages),
        { maxMessages: 10 }
      );

      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data).toHaveLength(10);
        expect(result.warnings).toHaveLength(1);
        expect(result.warnings[0].code).toBe('MESSAGE_LIMIT_EXCEEDED');
      }
    });
  });

  describe('validate', () => {
    it('should validate correct conversation structure', () => {
      const result = ConversationImporter.validate(validConversationJSON);

      expect(result.success).toBe(true);
      expect(result.data).toHaveLength(2);
      expect(result.errors).toBeUndefined();
    });

    it('should reject non-object data', () => {
      const result = ConversationImporter.validate('not an object');

      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.errors[0].code).toBe('INVALID_FORMAT');
      }
    });

    it('should reject data without messages array', () => {
      const result = ConversationImporter.validate({ version: '1.0' });

      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.errors[0].code).toBe('MISSING_MESSAGES');
      }
    });

    it('should validate individual messages', () => {
      const invalidData = {
        messages: [
          {
            // Missing required fields
            content: 'Hello'
          }
        ]
      };

      const result = ConversationImporter.validate(invalidData);

      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.errors.some(e => e.code === 'MISSING_REQUIRED_FIELD')).toBe(true);
      }
    });

    it('should allow partial validation', () => {
      const partialData = {
        messages: [
          {
            role: 'invalid_role',
            content: 'Hello'
          }
        ]
      };

      const result = ConversationImporter.validate(partialData, { 
        allowPartial: true 
      });

      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data).toHaveLength(1);
        expect(result.data[0].role).toBe('user'); // Should default invalid role
        expect(result.warnings.some(w => w.code === 'INVALID_ROLE')).toBe(true);
      }
    });

    it('should validate timestamps', () => {
      const dataWithInvalidTimestamp = {
        messages: [
          {
            id: 'msg1',
            role: 'user',
            content: 'Hello',
            timestamp: 'invalid-date'
          }
        ]
      };

      const result = ConversationImporter.validate(dataWithInvalidTimestamp);

      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.errors.some(e => e.code === 'INVALID_TIMESTAMP')).toBe(true);
      }
    });

    it('should handle missing timestamps with partial validation', () => {
      const dataWithoutTimestamp = {
        messages: [
          {
            id: 'msg1',
            role: 'user',
            content: 'Hello'
          }
        ]
      };

      const result = ConversationImporter.validate(dataWithoutTimestamp, {
        allowPartial: true
      });

      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data[0].timestamp).toBeInstanceOf(Date);
      }
    });
  });

  describe('sanitize', () => {
    it('should sanitize XSS content', () => {
      const unsafeMessages: ExportableMessage[] = [
        {
          id: 'msg1',
          role: 'user',
          content: '<script>alert("xss")</script>Hello',
          timestamp: new Date()
        }
      ];

      const sanitized = ConversationImporter.sanitize(unsafeMessages);

      expect(sanitized[0].content).toBe('Hello');
      expect(sanitized[0].content).not.toContain('<script>');
    });

    it('should remove javascript protocols', () => {
      const unsafeMessages: ExportableMessage[] = [
        {
          id: 'msg1',
          role: 'user',
          content: 'Click here: javascript:alert("xss")',
          timestamp: new Date()
        }
      ];

      const sanitized = ConversationImporter.sanitize(unsafeMessages);

      expect(sanitized[0].content).not.toContain('javascript:');
    });

    it('should generate IDs for messages without them', () => {
      const messagesWithoutId: ExportableMessage[] = [
        {
          id: '',
          role: 'user',
          content: 'Hello',
          timestamp: new Date()
        }
      ];

      const sanitized = ConversationImporter.sanitize(messagesWithoutId);

      expect(sanitized[0].id).toBeTruthy();
      expect(sanitized[0].id).toMatch(/^msg_/);
    });

    it('should validate and correct roles', () => {
      const messagesWithInvalidRole = [
        {
          id: 'msg1',
          role: 'invalid' as 'user' | 'assistant' | 'system',
          content: 'Hello',
          timestamp: new Date()
        }
      ];

      const sanitized = ConversationImporter.sanitize(messagesWithInvalidRole);

      expect(sanitized[0].role).toBe('user');
    });

    it('should ensure timestamps are Date objects', () => {
      const messagesWithStringTimestamp = [
        {
          id: 'msg1',
          role: 'user' as const,
          content: 'Hello',
          timestamp: '2024-01-01T10:00:00Z' as unknown as Date
        }
      ];

      const sanitized = ConversationImporter.sanitize(messagesWithStringTimestamp);

      expect(sanitized[0].timestamp).toBeInstanceOf(Date);
      expect(sanitized[0].timestamp.getTime()).toBe(new Date('2024-01-01T10:00:00Z').getTime());
    });
  });

  describe('round-trip export/import', () => {
    it('should successfully round-trip JSON export and import', () => {
      const originalMessages: ExportableMessage[] = [
        {
          id: 'msg1',
          role: 'user',
          content: 'Hello world!',
          timestamp: new Date('2024-01-01T10:00:00Z'),
          model: 'gpt-4',
          images: [
            {
              url: 'data:image/png;base64,abc123',
              width: 200,
              height: 150
            }
          ]
        },
        {
          id: 'msg2',
          role: 'assistant',
          content: 'Hello! How can I help you today?',
          timestamp: new Date('2024-01-01T10:00:05Z'),
          metadata: {
            tokensUsed: 12,
            latency: 1500,
            finishReason: 'stop'
          }
        }
      ];

      // Export to JSON
      const json = ConversationExporter.toJSON(originalMessages, {
        includeMetadata: true,
        includeTimestamps: true
      });

      // Import back from JSON
      const importResult = ConversationImporter.fromJSON(json);

      expect(importResult.success).toBe(true);
      if (importResult.success) {
        expect(importResult.data).toHaveLength(2);
        const imported = importResult.data;
        expect(imported[0].id).toBe('msg1');
        expect(imported[0].content).toBe('Hello world!');
        expect(imported[0].timestamp.getTime()).toBe(originalMessages[0].timestamp.getTime());
        expect(imported[0].images).toHaveLength(1);

        expect(imported[1].metadata?.tokensUsed).toBe(12);
        expect(imported[1].metadata?.latency).toBe(1500);
      }
    });
  });
});
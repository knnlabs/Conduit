/**
 * Unit tests for structured-content utilities
 */

import {
  processStructuredContent,
  getBlockQuoteMetadata,
  cleanBlockQuoteContent
} from '../structured-content';

describe('structured-content', () => {
  describe('processStructuredContent', () => {
    it('should return original content when no structured tags present', () => {
      const content = 'This is regular content without any special tags.';
      const result = processStructuredContent(content);
      expect(result).toBe(content);
    });

    it('should return empty string for empty input', () => {
      expect(processStructuredContent('')).toBe('');
      expect(processStructuredContent(null as any)).toBe(null);
      expect(processStructuredContent(undefined as any)).toBe(undefined);
    });

    it('should process thinking tags', () => {
      const content = '<thinking>This is my thought process</thinking>';
      const result = processStructuredContent(content);
      
      expect(result).toContain('> 💭 **Thinking...**');
      expect(result).toContain('> __collapse__');
      expect(result).toContain('> This is my thought process');
    });

    it('should process different thinking tag variations', () => {
      const variations = ['thinking', 'think', 'reasoning', 'antThinking'];
      
      variations.forEach(tag => {
        const content = `<${tag}>Content here</${tag}>`;
        const result = processStructuredContent(content);
        
        expect(result).toContain('> 💭 **Thinking...**');
        expect(result).toContain('> __collapse__');
        expect(result).toContain('> Content here');
      });
    });

    it('should process warning tags', () => {
      const content = '<warning>This is a warning message</warning>';
      const result = processStructuredContent(content);
      
      expect(result).toContain('> ⚠️ **Warning**');
      expect(result).toContain('> __warning__');
      expect(result).toContain('> This is a warning message');
    });

    it('should process different warning tag variations', () => {
      const variations = ['warning', 'caution', 'alert'];
      
      variations.forEach(tag => {
        const content = `<${tag}>Be careful here</${tag}>`;
        const result = processStructuredContent(content);
        
        expect(result).toContain('> ⚠️ **Warning**');
        expect(result).toContain('> __warning__');
        expect(result).toContain('> Be careful here');
      });
    });

    it('should process summary tags', () => {
      const content = '<summary>This is the summary</summary>';
      const result = processStructuredContent(content);
      
      expect(result).toContain('> 📋 **Summary**');
      expect(result).toContain('> __summary__');
      expect(result).toContain('> This is the summary');
    });

    it('should process answer tags as summary', () => {
      const content = '<answer>This is the answer</answer>';
      const result = processStructuredContent(content);
      
      expect(result).toContain('> 📋 **Summary**');
      expect(result).toContain('> __summary__');
      expect(result).toContain('> This is the answer');
    });

    it('should handle multiline content within tags', () => {
      const content = `<thinking>
First line of thinking
Second line of thinking
Third line
</thinking>`;
      
      const result = processStructuredContent(content);
      
      expect(result).toContain('> First line of thinking');
      expect(result).toContain('> Second line of thinking');
      expect(result).toContain('> Third line');
    });

    it('should process emoji-based warnings', () => {
      const content1 = '⚠️ Warning: This is important';
      const content2 = '🚨 Important: Pay attention';
      
      const result1 = processStructuredContent(content1);
      const result2 = processStructuredContent(content2);
      
      expect(result1).toContain('> ⚠️ **Warning**');
      expect(result1).toContain('> __warning__');
      expect(result1).toContain('> This is important');
      
      expect(result2).toContain('> ⚠️ **Warning**');
      expect(result2).toContain('> __warning__');
      expect(result2).toContain('> Pay attention');
    });

    it('should process multiple different tags in same content', () => {
      const content = `
Regular text here.

<thinking>My thought process</thinking>

More regular text.

<warning>Be careful</warning>

<summary>Final summary</summary>
`;
      
      const result = processStructuredContent(content);
      
      expect(result).toContain('> 💭 **Thinking...**');
      expect(result).toContain('> My thought process');
      expect(result).toContain('> ⚠️ **Warning**');
      expect(result).toContain('> Be careful');
      expect(result).toContain('> 📋 **Summary**');
      expect(result).toContain('> Final summary');
    });

    it('should be case insensitive for tag matching', () => {
      const content = '<THINKING>Upper case tag</THINKING>';
      const result = processStructuredContent(content);
      
      expect(result).toContain('> 💭 **Thinking...**');
      expect(result).toContain('> Upper case tag');
    });
  });

  describe('getBlockQuoteMetadata', () => {
    it('should identify thinking blockquotes', () => {
      const text = '> 💭 **Thinking...**\n> __collapse__\n> Some thoughts';
      const metadata = getBlockQuoteMetadata(text);
      
      expect(metadata.type).toBe('thinking');
      expect(metadata.icon).toBe('💭');
      expect(metadata.title).toBe('Thinking...');
      expect(metadata.collapsible).toBe(true);
    });

    it('should identify warning blockquotes', () => {
      const text = '> ⚠️ **Warning**\n> __warning__\n> Important message';
      const metadata = getBlockQuoteMetadata(text);
      
      expect(metadata.type).toBe('warning');
      expect(metadata.icon).toBe('⚠️');
      expect(metadata.title).toBe('Warning');
      expect(metadata.collapsible).toBeUndefined();
    });

    it('should identify summary blockquotes', () => {
      const text = '> 📋 **Summary**\n> __summary__\n> Key points';
      const metadata = getBlockQuoteMetadata(text);
      
      expect(metadata.type).toBe('summary');
      expect(metadata.icon).toBe('📋');
      expect(metadata.title).toBe('Summary');
    });

    it('should default to standard for unrecognized blockquotes', () => {
      const text = '> Just a regular blockquote';
      const metadata = getBlockQuoteMetadata(text);
      
      expect(metadata.type).toBe('standard');
      expect(metadata.icon).toBe('');
      expect(metadata.title).toBe('');
    });
  });

  describe('cleanBlockQuoteContent', () => {
    it('should remove thinking metadata markers', () => {
      const content = `💭 **Thinking...**
__collapse__
This is the actual content
More content`;
      
      const cleaned = cleanBlockQuoteContent(content);
      
      expect(cleaned).not.toContain('💭 **Thinking...**');
      expect(cleaned).not.toContain('__collapse__');
      expect(cleaned).toContain('This is the actual content');
      expect(cleaned).toContain('More content');
    });

    it('should remove warning metadata markers', () => {
      const content = `⚠️ **Warning**
__warning__
This is the warning message`;
      
      const cleaned = cleanBlockQuoteContent(content);
      
      expect(cleaned).not.toContain('⚠️ **Warning**');
      expect(cleaned).not.toContain('__warning__');
      expect(cleaned).toContain('This is the warning message');
    });

    it('should remove summary metadata markers', () => {
      const content = `📋 **Summary**
__summary__
This is the summary content`;
      
      const cleaned = cleanBlockQuoteContent(content);
      
      expect(cleaned).not.toContain('📋 **Summary**');
      expect(cleaned).not.toContain('__summary__');
      expect(cleaned).toContain('This is the summary content');
    });

    it('should handle content without metadata markers', () => {
      const content = 'Just regular blockquote content';
      const cleaned = cleanBlockQuoteContent(content);
      
      expect(cleaned).toBe(content);
    });

    it('should trim whitespace after cleaning', () => {
      const content = `💭 **Thinking...**
__collapse__
  Content with whitespace  
  `;
      
      const cleaned = cleanBlockQuoteContent(content);
      
      expect(cleaned).toBe('Content with whitespace');
    });
  });
});
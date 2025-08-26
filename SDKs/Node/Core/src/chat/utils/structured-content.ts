/**
 * Preprocesses structured content tags from AI responses into markdown-friendly formats
 * This handles common XML-like tags used by modern LLMs and converts them to enhanced markdown
 * Extracted from WebUI for reuse in other applications
 */

export interface ProcessedBlockQuote {
  type: 'thinking' | 'warning' | 'summary' | 'standard';
  icon: string;
  title: string;
  collapsible?: boolean;
}

/**
 * Processes structured content by converting XML-like tags to markdown blockquotes
 * @param content The raw content from the AI response
 * @returns The processed content with tags converted to markdown
 */
export function processStructuredContent(content: string): string {
  if (!content) return content;

  let processedContent = content;

  // Process thinking/reasoning tags - convert to collapsible sections
  processedContent = processedContent.replace(
    /<(thinking|think|reasoning|antThinking)>([\s\S]*?)<\/\1>/gi,
    (_match, _tag, innerContent: string) => {
      const innerContentStr = String(innerContent);
      return `> 💭 **Thinking...**\n> __collapse__\n${innerContentStr.trim().split('\n').map((line: string) => `> ${line}`).join('\n')}\n`;
    }
  );

  // Process warning/alert tags
  processedContent = processedContent.replace(
    /<(warning|caution|alert)>([\s\S]*?)<\/\1>/gi,
    (_match, _tag, innerContent: string) => {
      const innerContentStr = String(innerContent);
      return `> ⚠️ **Warning**\n> __warning__\n${innerContentStr.trim().split('\n').map((line: string) => `> ${line}`).join('\n')}\n`;
    }
  );

  // Process summary/answer tags
  processedContent = processedContent.replace(
    /<(summary|answer)>([\s\S]*?)<\/\1>/gi,
    (_match, _tag, innerContent: string) => {
      const innerContentStr = String(innerContent);
      return `> 📋 **Summary**\n> __summary__\n${innerContentStr.trim().split('\n').map((line: string) => `> ${line}`).join('\n')}\n`;
    }
  );

  // Process emoji-based warnings
  processedContent = processedContent.replace(
    /^(⚠️\s*Warning:|🚨\s*Important:)\s*(.*)$/gm,
    (_match, _prefix, text: string) => {
      return `> ⚠️ **Warning**\n> __warning__\n> ${text}`;
    }
  );

  return processedContent;
}

/**
 * Extracts metadata from a blockquote to determine its type
 * @param text The text content of the blockquote
 * @returns Metadata about the blockquote type
 */
export function getBlockQuoteMetadata(text: string): ProcessedBlockQuote {
  // Check for thinking pattern
  if (text.includes('💭 **Thinking') && text.includes('__collapse__')) {
    return {
      type: 'thinking',
      icon: '💭',
      title: 'Thinking...',
      collapsible: true
    };
  }

  // Check for warning pattern
  if (text.includes('⚠️ **Warning') && text.includes('__warning__')) {
    return {
      type: 'warning',
      icon: '⚠️',
      title: 'Warning'
    };
  }

  // Check for summary pattern
  if (text.includes('📋 **Summary') && text.includes('__summary__')) {
    return {
      type: 'summary',
      icon: '📋',
      title: 'Summary'
    };
  }

  // Default blockquote
  return {
    type: 'standard',
    icon: '',
    title: ''
  };
}

/**
 * Cleans blockquote content by removing metadata markers
 * @param content The blockquote content
 * @returns The cleaned content
 */
export function cleanBlockQuoteContent(content: string): string {
  return content
    .replace(/^💭\s*\*\*Thinking\.\.\.\*\*\s*$/m, '')
    .replace(/^⚠️\s*\*\*Warning\*\*\s*$/m, '')
    .replace(/^📋\s*\*\*Summary\*\*\s*$/m, '')
    .replace(/^__collapse__\s*$/m, '')
    .replace(/^__warning__\s*$/m, '')
    .replace(/^__summary__\s*$/m, '')
    .trim();
}
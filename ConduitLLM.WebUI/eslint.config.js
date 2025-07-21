import js from '@eslint/js';
import typescriptPlugin from '@typescript-eslint/eslint-plugin';
import typescriptParser from '@typescript-eslint/parser';
import eslintCommentsPlugin from 'eslint-plugin-eslint-comments';
import nextPlugin from '@next/eslint-plugin-next';
import reactPlugin from 'eslint-plugin-react';
import reactHooksPlugin from 'eslint-plugin-react-hooks';

export default [
  js.configs.recommended,
  {
    files: ['**/*.ts', '**/*.tsx'],
    languageOptions: {
      parser: typescriptParser,
      ecmaVersion: 'latest',
      sourceType: 'module',
      parserOptions: {
        project: './tsconfig.json',
        ecmaFeatures: {
          jsx: true,
        },
      },
      globals: {
        React: 'readonly',
        JSX: 'readonly',
        console: 'readonly',
        process: 'readonly',
        Buffer: 'readonly',
        __dirname: 'readonly',
        __filename: 'readonly',
        exports: 'writeable',
        module: 'writeable',
        require: 'readonly',
        global: 'readonly',
        fetch: 'readonly',
        setTimeout: 'readonly',
        clearTimeout: 'readonly',
        setInterval: 'readonly',
        clearInterval: 'readonly',
        window: 'readonly',
        document: 'readonly',
        navigator: 'readonly',
        location: 'readonly',
        URL: 'readonly',
        URLSearchParams: 'readonly',
        AbortController: 'readonly',
        AbortSignal: 'readonly',
        FormData: 'readonly',
        Headers: 'readonly',
        Request: 'readonly',
        Response: 'readonly',
        EventSource: 'readonly',
        WebSocket: 'readonly',
        localStorage: 'readonly',
        sessionStorage: 'readonly',
        alert: 'readonly',
      },
    },
    plugins: {
      '@typescript-eslint': typescriptPlugin,
      'eslint-comments': eslintCommentsPlugin,
      '@next/next': nextPlugin,
      'react': reactPlugin,
      'react-hooks': reactHooksPlugin,
    },
    rules: {
      ...typescriptPlugin.configs.recommended.rules,
      ...typescriptPlugin.configs['recommended-requiring-type-checking'].rules,
      ...nextPlugin.configs['core-web-vitals'].rules,
      
      // TypeScript specific rules
      '@typescript-eslint/no-unused-vars': 'error',
      '@typescript-eslint/explicit-function-return-type': 'off',
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-non-null-assertion': 'warn',
      '@typescript-eslint/strict-boolean-expressions': 'off',
      '@typescript-eslint/no-floating-promises': 'warn',
      '@typescript-eslint/no-misused-promises': 'warn',
      '@typescript-eslint/await-thenable': 'warn',
      '@typescript-eslint/no-unsafe-assignment': 'error',
      '@typescript-eslint/no-unsafe-member-access': 'error',
      '@typescript-eslint/no-unsafe-call': 'error',
      '@typescript-eslint/no-unsafe-return': 'error',
      '@typescript-eslint/require-await': 'off',
      '@typescript-eslint/no-unnecessary-type-assertion': 'warn',
      '@typescript-eslint/prefer-nullish-coalescing': 'warn',
      '@typescript-eslint/prefer-optional-chain': 'warn',
      '@typescript-eslint/no-unsafe-argument': 'error',
      '@typescript-eslint/consistent-type-assertions': [
        'error',
        {
          'assertionStyle': 'as',
          'objectLiteralTypeAssertions': 'allow'
        }
      ],
      
      // Prevent underscore prefix bypasses
      '@typescript-eslint/naming-convention': [
        'error',
        {
          'selector': 'variable',
          'format': ['camelCase', 'PascalCase', 'UPPER_CASE', 'snake_case'],
          'leadingUnderscore': 'forbid'
        },
        {
          'selector': 'parameter',
          'format': ['camelCase', 'PascalCase', 'snake_case'],
          'leadingUnderscore': 'forbid'
        },
        {
          'selector': 'property',
          'format': ['camelCase', 'PascalCase', 'UPPER_CASE', 'snake_case'],
          'leadingUnderscore': 'forbid',
          'filter': {
            'regex': '^(Content-Type|Content-Disposition|content-type|content-disposition|max_tokens|top_p|presence_penalty|response_format|aspect_ratio|webhook_url|supportsFunctionCalling|supportsVision|supportsImageGeneration|supportsAudioTranscription|supportsTextToSpeech|supportsRealtimeAudio|supportsStreaming|supportsVideoGeneration|supportsEmbeddings|maxContextLength|maxOutputTokens|isDefault|defaultCapabilityType|_note)$',
            'match': false
          }
        }
      ],
      
      // Prevent ESLint disable bypasses - STRICTLY ENFORCED
      'eslint-comments/no-unlimited-disable': 'error',
      'eslint-comments/no-unused-disable': 'error', 
      'eslint-comments/disable-enable-pair': 'error',
      'eslint-comments/no-duplicate-disable': 'error',
      'eslint-comments/no-restricted-disable': [
        'error',
        '@typescript-eslint/no-unsafe-*',
        '@typescript-eslint/naming-convention',
        '@typescript-eslint/no-explicit-any'
      ],
      
      // General code quality rules
      'no-unused-vars': 'off', // Use TypeScript's no-unused-vars instead
      'no-console': ['warn', { 'allow': ['warn', 'error'] }],
      'eqeqeq': ['error', 'always'],
      'no-debugger': 'error',
      'no-alert': 'error',
      'no-var': 'error',
      'prefer-const': 'error',
      'prefer-arrow-callback': 'error',
      'no-param-reassign': 'error',
      'no-return-await': 'error',
      'require-await': 'off',
      'no-nested-ternary': 'warn',
      'no-unneeded-ternary': 'error',
      'prefer-template': 'error',
      'no-duplicate-imports': 'warn',
      
      // React specific rules
      'react/no-array-index-key': 'warn',
      'react/jsx-no-useless-fragment': 'error',
      'react-hooks/exhaustive-deps': 'error',
      
      // Import rules
      'import/order': 'off'
    },
  },
  {
    files: ['*.js'],
    rules: {
      '@typescript-eslint/explicit-function-return-type': 'off'
    }
  },
  {
    ignores: ['eslint.config.js', '.eslintrc.json', '.next/', 'out/', 'node_modules/', '*.js', '!*.config.js'],
  },
];
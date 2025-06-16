import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

/**
 * Creating a sidebar enables you to:
 - create an ordered group of docs
 - render a sidebar for each doc of that group
 - provide next/previous navigation

 The sidebars can be generated from the filesystem, or explicitly defined here.

 Create as many sidebars as you want.
 */
const sidebars: SidebarsConfig = {
  docsSidebar: [
    {
      type: 'doc',
      id: 'intro',
      label: 'Introduction',
    },
    {
      type: 'category',
      label: 'Getting Started',
      collapsed: false,
      items: [
        'getting-started/installation',
        'getting-started/quick-start',
        'getting-started/configuration',
      ],
    },
    {
      type: 'category',
      label: 'Features',
      items: [
        'features/api-gateway',
        'features/virtual-keys',
        'features/model-routing',
        'features/provider-integration',
        'features/multimodal-support',
        'features/audio-services',
        'features/audio-providers',
        'features/correlation-tracing',
      ],
    },
    {
      type: 'category',
      label: 'Guides',
      items: [
        'guides/environment-variables',
        'guides/cache-configuration',
        'guides/budget-management',
        'guides/webui-usage',
      ],
    },
    {
      type: 'category',
      label: 'API Reference',
      items: [
        'api-reference/overview',
        'api-reference/chat-completions',
        'api-reference/embeddings',
        'api-reference/models',
        'api-reference/audio',
      ],
    },
    {
      type: 'category',
      label: 'Architecture',
      items: [
        'architecture/overview',
        'architecture/components',
        'architecture/repository-pattern',
      ],
    },
    {
      type: 'category',
      label: 'Monitoring & Operations',
      items: [
        'monitoring/health-checks',
        'monitoring/metrics-monitoring',
        'monitoring/production-deployment',
        'monitoring/runbooks',
      ],
    },
    {
      type: 'category',
      label: 'Troubleshooting',
      items: [
        'troubleshooting/common-issues',
        'troubleshooting/faq',
      ],
    },
  ],
};

export default sidebars;
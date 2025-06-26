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
      label: 'Core APIs',
      items: [
        'core-apis/overview',
      ],
    },
    {
      type: 'category',
      label: 'Audio Platform',
      items: [
        'audio/text-to-speech',
        'audio/real-time-audio',
      ],
    },
    {
      type: 'category',
      label: 'Media Generation',
      items: [
        'media/image-generation',
        'media/video-generation',
        'media/async-processing',
      ],
    },
    {
      type: 'category',
      label: 'Administration',
      items: [
        'admin/admin-api-overview',
        'admin/virtual-keys',
        'admin/provider-configuration',
        'admin/usage-billing',
      ],
    },
  ],
};

export default sidebars;
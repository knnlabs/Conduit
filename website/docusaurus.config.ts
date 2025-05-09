import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const config: Config = {
  title: 'Conduit Documentation',
  tagline: 'A unified API gateway for multiple LLM providers',
  favicon: 'img/favicon.ico',

  // Set the production url of your site here
  url: 'https://knnlabs.github.io',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub pages deployment, it is often '/<projectName>/'
  baseUrl: '/Conduit/', // Changed to match exact case of repository name

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'knnlabs', // Usually your GitHub org/user name.
  projectName: 'Conduit', // Changed to match exact case of repository name

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  // Enable Mermaid diagrams
  markdown: {
    mermaid: true,
  },
  themes: ['@docusaurus/theme-mermaid'],

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          // Set the edit URL to the main repository
          editUrl: 'https://github.com/knnlabs/Conduit/tree/main/website/',
        },
        blog: false, // Disable blog
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    // Replace with your project's social card
    image: 'img/conduit-social-card.png',
    navbar: {
      title: 'Conduit',
      logo: {
        alt: 'Conduit Logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Documentation',
        },
        {
          href: 'https://github.com/knnlabs/conduit',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {
              label: 'Introduction',
              to: '/docs/intro',
            },
            {
              label: 'Getting Started',
              to: '/docs/getting-started/installation',
            },
            {
              label: 'API Reference',
              to: '/docs/api-reference/overview',
            },
          ],
        },
        {
          title: 'Community',
          items: [
            {
              label: 'GitHub Discussions',
              href: 'https://github.com/knnlabs/conduit/discussions',
            },
            {
              label: 'Issues',
              href: 'https://github.com/knnlabs/conduit/issues',
            },
          ],
        },
        {
          title: 'More',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/knnlabs/conduit',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} KNN Labs, Inc.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash', 'json', 'yaml', 'powershell'],
    },
    colorMode: {
      defaultMode: 'light',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    docs: {
      sidebar: {
        hideable: true,
        autoCollapseCategories: true,
      },
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
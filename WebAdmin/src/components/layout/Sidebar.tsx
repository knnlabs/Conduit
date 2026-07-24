'use client';

import { ScrollArea, NavLink, Stack, ThemeIcon, Text, Divider } from '@mantine/core';
import {
  IconDashboard,
  IconKey,
  IconChartBar,
  IconRoute,
  IconServer,
  IconDatabase,
  IconBugOff,
  IconShield,
  IconPhoto,
  IconVideo,
  IconMessage,
  IconCoin,
  IconInfoCircle,
  IconEye,
  IconUsers,
  IconBrain,
  IconTool,
  IconSettings,
  IconActivity,
  IconListDetails,
  IconBolt
} from '@tabler/icons-react';
import { useRouter, usePathname } from 'next/navigation';

const navigationSections = [
  {
    title: 'Core Management',
    items: [
      { id: 'dashboard', label: 'Dashboard', href: '/', icon: IconDashboard },
      { id: 'virtualkeys', label: 'Virtual Keys', href: '/virtualkeys', icon: IconKey },
      { id: 'virtualkeys-groups', label: 'Virtual Key Groups', href: '/virtualkeys/groups', icon: IconUsers },
      { id: 'request-logs', label: 'Request Logs', href: '/request-logs', icon: IconListDetails },
      { id: 'usage-analytics', label: 'Usage Analytics', href: '/usage-analytics', icon: IconChartBar },
    ]
  },
  {
    title: 'Provider & Model Management',
    items: [
      { id: 'models', label: 'Models', href: '/models', icon: IconBrain },
      { id: 'llm-providers', label: 'LLM Providers', href: '/llm-providers', icon: IconServer },
      { id: 'model-mappings', label: 'Model Mappings', href: '/model-mappings', icon: IconRoute },
      { id: 'provider-tools', label: 'Provider Tools', href: '/provider-tools', icon: IconTool },
      { id: 'prompt-caching', label: 'Prompt Caching', href: '/prompt-caching', icon: IconBolt },
    ]
  },
  {
    title: 'Functions',
    items: [
      { id: 'functions-configurations', label: 'Configurations', href: '/functions/configurations', icon: IconSettings },
      { id: 'functions-credentials', label: 'Credentials', href: '/functions/credentials', icon: IconKey },
      { id: 'functions-costs', label: 'Cost Management', href: '/functions/costs', icon: IconCoin },
      { id: 'functions-executions', label: 'Executions', href: '/functions/executions', icon: IconActivity },
    ]
  },
  {
    title: 'Security & Monitoring',
    items: [
      { id: 'ip-filtering', label: 'IP Filtering', href: '/ip-filtering', icon: IconShield },
      { id: 'system-info', label: 'System Info', href: '/system-info', icon: IconInfoCircle },
      { id: 'settings', label: 'Settings', href: '/settings', icon: IconSettings },
      { id: 'virtualkeys-discovery-preview', label: 'Discovery Preview', href: '/virtualkeys/discovery-preview', icon: IconEye },
      { id: 'media-assets', label: 'Media Assets', href: '/media-assets', icon: IconDatabase },
      { id: 'media-cleanup', label: 'Media Cleanup', href: '/media-assets/cleanup-status', icon: IconCoin },
      { id: 'provider-errors', label: 'Provider Errors', href: '/provider-errors', icon: IconBugOff },
    ]
  },
  {
    title: 'Playground',
    items: [
      { id: 'images', label: 'Images', href: '/images', icon: IconPhoto },
      { id: 'videos', label: 'Videos', href: '/videos', icon: IconVideo },
      { id: 'chat', label: 'Chat', href: '/chat', icon: IconMessage },
    ]
  }
];

export function Sidebar() {
  const router = useRouter();
  const pathname = usePathname();

  const handleItemClick = (href: string) => {
    router.push(href);
  };

  return (
    <ScrollArea h="100%" p="md">
      <Stack gap="lg">
        {navigationSections.map((section, sectionIndex) => (
          <div key={section.title}>
            <Text size="xs" fw={600} c="dimmed" tt="uppercase" mb="xs">
              {section.title}
            </Text>
            <Stack gap={2}>
              {section.items.map((item) => (
                <NavLink
                  key={item.id}
                  label={item.label}
                  leftSection={
                    <ThemeIcon size="sm" variant={pathname === item.href ? 'filled' : 'light'}>
                      <item.icon size={16} />
                    </ThemeIcon>
                  }
                  active={pathname === item.href}
                  onClick={() => handleItemClick(item.href)}
                />
              ))}
            </Stack>
            {sectionIndex < navigationSections.length - 1 && <Divider mt="md" />}
          </div>
        ))}
      </Stack>
    </ScrollArea>
  );
}

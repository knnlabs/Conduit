'use client';

import { ScrollArea, NavLink, Stack, ThemeIcon, Text, Divider } from '@mantine/core';
import { 
  IconDashboard, 
  IconKey, 
  IconSettings,
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
  IconBrain
} from '@tabler/icons-react';
import { useRouter, usePathname } from 'next/navigation';

const navigationSections = [
  {
    title: 'Core Management',
    items: [
      { id: 'dashboard', label: 'Dashboard', href: '/', icon: IconDashboard },
      { id: 'virtualkeys', label: 'Virtual Keys', href: '/virtualkeys', icon: IconKey },
      { id: 'virtualkeys-groups', label: 'Virtual Key Groups', href: '/virtualkeys/groups', icon: IconUsers },
      { id: 'virtualkeys-discovery-preview', label: 'Discovery Preview', href: '/virtualkeys/discovery-preview', icon: IconEye },
    ]
  },
  {
    title: 'Provider & Model Management',
    items: [
      { id: 'models', label: 'Models', href: '/models', icon: IconBrain },
      { id: 'llm-providers', label: 'LLM Providers', href: '/llm-providers', icon: IconServer },
      { id: 'model-mappings', label: 'Model Mappings', href: '/model-mappings', icon: IconRoute },
      { id: 'routing-settings', label: 'Routing Settings', href: '/routing-settings', icon: IconSettings },
    ]
  },
  {
    title: 'Security & Monitoring',
    items: [
      { id: 'ip-filtering', label: 'IP Filtering', href: '/ip-filtering', icon: IconShield },
      { id: 'system-info', label: 'System Info', href: '/system-info', icon: IconInfoCircle },
      { id: 'error-queues', label: 'Error Queues', href: '/error-queues', icon: IconBugOff },
      { id: 'provider-errors', label: 'Provider Errors', href: '/provider-errors', icon: IconBugOff },
    ]
  },
  {
    title: 'Media & Communication',
    items: [
      { id: 'images', label: 'Images', href: '/images', icon: IconPhoto },
      { id: 'videos', label: 'Videos', href: '/videos', icon: IconVideo },
      { id: 'chat', label: 'Chat', href: '/chat', icon: IconMessage },
      { id: 'media-assets', label: 'Media Assets', href: '/media-assets', icon: IconDatabase },
    ]
  },
  {
    title: 'Analytics & Reporting',
    items: [
      { id: 'usage-analytics', label: 'Usage Analytics', href: '/usage-analytics', icon: IconChartBar },
      { id: 'cost-dashboard', label: 'Cost Dashboard', href: '/cost-dashboard', icon: IconCoin },
      { id: 'model-costs', label: 'Model Pricing', href: '/model-costs', icon: IconCoin },
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
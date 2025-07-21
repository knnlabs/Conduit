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
  IconActivity,
  IconFileText,
  IconCoin,
  IconInfoCircle
} from '@tabler/icons-react';
import { useRouter, usePathname } from 'next/navigation';

const navigationSections = [
  {
    title: 'Core Management',
    items: [
      { id: 'dashboard', label: 'Dashboard', href: '/', icon: IconDashboard },
      { id: 'virtualkeys', label: 'Virtual Keys', href: '/virtualkeys', icon: IconKey },
      { id: 'virtualkeys-dashboard', label: 'Virtual Keys Dashboard', href: '/virtualkeys/dashboard', icon: IconChartBar },
      { id: 'virtual-keys-analytics', label: 'Virtual Keys Analytics', href: '/virtual-keys-analytics', icon: IconChartBar },
    ]
  },
  {
    title: 'Provider & Model Management',
    items: [
      { id: 'llm-providers', label: 'LLM Providers', href: '/llm-providers', icon: IconServer },
      { id: 'provider-health', label: 'Provider Health', href: '/provider-health', icon: IconActivity },
      { id: 'model-mappings', label: 'Model Mappings', href: '/model-mappings', icon: IconRoute },
      { id: 'routing-settings', label: 'Routing Settings', href: '/routing-settings', icon: IconSettings },
    ]
  },
  {
    title: 'Caching & Performance',
    items: [
      { id: 'caching-settings', label: 'Caching Settings', href: '/caching-settings', icon: IconDatabase },
      { id: 'cache-monitoring', label: 'Cache Monitoring', href: '/cache-monitoring', icon: IconDatabase },
      { id: 'error-queues', label: 'Error Queues', href: '/error-queues', icon: IconBugOff },
    ]
  },
  {
    title: 'Security & Monitoring',
    items: [
      { id: 'ip-filtering', label: 'IP Filtering', href: '/ip-filtering', icon: IconShield },
      { id: 'request-logs', label: 'Request Logs', href: '/request-logs', icon: IconFileText },
      { id: 'health-monitoring', label: 'Health Monitoring', href: '/health-monitoring', icon: IconActivity },
      { id: 'system-performance', label: 'System Performance', href: '/system-performance', icon: IconActivity },
      { id: 'system-info', label: 'System Info', href: '/system-info', icon: IconInfoCircle },
    ]
  },
  {
    title: 'Media & Communication',
    items: [
      { id: 'images', label: 'Images', href: '/images', icon: IconPhoto },
      { id: 'videos', label: 'Videos', href: '/videos', icon: IconVideo },
      { id: 'chat', label: 'Chat', href: '/chat', icon: IconMessage },
    ]
  },
  {
    title: 'Analytics & Reporting',
    items: [
      { id: 'usage-analytics', label: 'Usage Analytics', href: '/usage-analytics', icon: IconChartBar },
      { id: 'cost-dashboard', label: 'Cost Dashboard', href: '/cost-dashboard', icon: IconCoin },
      { id: 'model-costs', label: 'Model Pricing', href: '/model-costs', icon: IconCoin },
      { id: 'configuration', label: 'Configuration', href: '/configuration', icon: IconSettings },
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
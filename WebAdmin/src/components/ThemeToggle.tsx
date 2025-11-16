'use client';

import { ActionIcon, Tooltip, Menu, Group } from '@mantine/core';
import { IconSun, IconMoon, IconDeviceDesktop, IconPalette } from '@tabler/icons-react';
import { useTheme } from '@/contexts/ThemeContext';

export function ThemeToggle() {
  const { mode, colorScheme, setMode } = useTheme();

  const getIcon = () => {
    if (mode === 'auto') {
      return <IconDeviceDesktop size={18} />;
    }
    return colorScheme === 'dark' ? <IconMoon size={18} /> : <IconSun size={18} />;
  };

  const getTooltipLabel = () => {
    switch (mode) {
      case 'light':
        return 'Light theme';
      case 'dark':
        return 'Dark theme';
      case 'auto':
        return `Auto (${colorScheme === 'dark' ? 'Dark' : 'Light'})`;
      default:
        return 'Theme';
    }
  };

  return (
    <Menu shadow="md" width={160} position="bottom-end">
      <Menu.Target>
        <Tooltip label={getTooltipLabel()}>
          <ActionIcon
            variant="subtle"
            size="lg"
            aria-label="Theme selector"
          >
            {getIcon()}
          </ActionIcon>
        </Tooltip>
      </Menu.Target>

      <Menu.Dropdown>
        <Menu.Label>
          <Group gap="xs">
            <IconPalette size={16} />
            Theme
          </Group>
        </Menu.Label>
        
        <Menu.Item
          leftSection={<IconSun size={16} />}
          onClick={() => setMode('light')}
          rightSection={mode === 'light' ? '✓' : undefined}
          color={mode === 'light' ? 'blue' : undefined}
        >
          Light
        </Menu.Item>
        
        <Menu.Item
          leftSection={<IconMoon size={16} />}
          onClick={() => setMode('dark')}
          rightSection={mode === 'dark' ? '✓' : undefined}
          color={mode === 'dark' ? 'blue' : undefined}
        >
          Dark
        </Menu.Item>
        
        <Menu.Item
          leftSection={<IconDeviceDesktop size={16} />}
          onClick={() => setMode('auto')}
          rightSection={mode === 'auto' ? '✓' : undefined}
          color={mode === 'auto' ? 'blue' : undefined}
        >
          System {mode === 'auto' && `(${colorScheme === 'dark' ? 'Dark' : 'Light'})`}
        </Menu.Item>
      </Menu.Dropdown>
    </Menu>
  );
}
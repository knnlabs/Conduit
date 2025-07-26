'use client';

import {
  Group,
  Burger,
  Text,
  ActionIcon,
  Divider,
} from '@mantine/core';
import {
  IconBell,
} from '@tabler/icons-react';
import { UserButton } from '@clerk/nextjs';
import { ConnectionIndicator } from './ConnectionIndicator';
import { ThemeToggle } from '@/components/ThemeToggle';

interface HeaderProps {
  mobileOpened: boolean;
  desktopOpened: boolean;
  toggleMobile: () => void;
  toggleDesktop: () => void;
}

export function Header({
  mobileOpened,
  desktopOpened,
  toggleMobile,
  toggleDesktop,
}: HeaderProps) {
  return (
    <Group h="100%" px="md" justify="space-between">
      <Group>
        <Burger
          opened={mobileOpened}
          onClick={toggleMobile}
          hiddenFrom="sm"
          size="sm"
        />
        <Burger
          opened={desktopOpened}
          onClick={toggleDesktop}
          visibleFrom="sm"
          size="sm"
        />
        
        <Text size="lg" fw={600} c="blue">
          Conduit WebUI
        </Text>
      </Group>

      <Group>
        <ConnectionIndicator />
        
        <Divider orientation="vertical" />
        
        <ThemeToggle />
        
        <ActionIcon variant="light" size="lg">
          <IconBell size={18} />
        </ActionIcon>

        <UserButton />
      </Group>
    </Group>
  );
}
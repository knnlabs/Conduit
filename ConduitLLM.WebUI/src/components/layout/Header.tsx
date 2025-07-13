'use client';

import {
  Group,
  Burger,
  Text,
  ActionIcon,
  Menu,
  Avatar,
  UnstyledButton,
  rem,
  Divider,
} from '@mantine/core';
import {
  IconBell,
  IconSettings,
  IconLogout,
  IconUser,
  IconChevronDown,
} from '@tabler/icons-react';
import { useRouter } from 'next/navigation';
import { UserButton, useAuth as useClerkAuth } from '@clerk/nextjs';
import { useAuthStore } from '@/stores/useAuthStore';
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
  const router = useRouter();
  const { user: _user, logout } = useAuthStore();
  const { isSignedIn } = useClerkAuth();
  const authType = process.env.NEXT_PUBLIC_CONDUIT_AUTH_TYPE ?? 'password';

  const handleLogout = () => {
    logout();
    router.push('/login');
  };


  const handleSettings = () => {
    router.push('/configuration');
  };

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

        {authType === 'clerk' ? (
          <UserButton afterSignOutUrl="/sign-in" />
        ) : (
        <Menu shadow="md" width={200} position="bottom-end">
          <Menu.Target>
            <UnstyledButton>
              <Group gap="xs">
                <Avatar size="sm" color="blue">
                  <IconUser size={16} />
                </Avatar>
                <Text size="sm" fw={500}>
                  Admin
                </Text>
                <IconChevronDown size={12} />
              </Group>
            </UnstyledButton>
          </Menu.Target>

          <Menu.Dropdown>
            <Menu.Item
              leftSection={<IconSettings style={{ width: rem(14), height: rem(14) }} />}
              onClick={handleSettings}
            >
              Settings
            </Menu.Item>
            
            <Menu.Divider />
            
            <Menu.Item
              leftSection={<IconLogout style={{ width: rem(14), height: rem(14) }} />}
              color="red"
              onClick={handleLogout}
            >
              Sign out
            </Menu.Item>
          </Menu.Dropdown>
        </Menu>) }
      </Group>
    </Group>
  );
}
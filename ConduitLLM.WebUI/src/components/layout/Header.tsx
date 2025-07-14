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
import { useAuthStore } from '@/stores/useAuthStore';
import { useClerk, useUser } from '@clerk/nextjs';
import { ConnectionIndicator } from './ConnectionIndicator';
import { ThemeToggle } from '@/components/ThemeToggle';
import { getAuthMode } from '@/lib/auth/auth-mode';

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
  const authMode = getAuthMode();
  
  // Conduit auth
  const { user: conduitUser, logout: conduitLogout } = useAuthStore();
  
  // Clerk auth
  const { signOut } = useClerk();
  const { user: clerkUser } = useUser();

  const handleLogout = async () => {
    if (authMode === 'clerk') {
      await signOut();
    } else {
      conduitLogout();
      router.push('/login');
    }
  };

  const handleSettings = () => {
    router.push('/configuration');
  };

  // Get display name based on auth mode
  const getDisplayName = () => {
    if (authMode === 'clerk' && clerkUser) {
      return clerkUser.firstName || clerkUser.emailAddresses[0]?.emailAddress || 'Admin';
    }
    return 'Admin';
  };

  // Get avatar content based on auth mode
  const getAvatarContent = () => {
    if (authMode === 'clerk' && clerkUser?.imageUrl) {
      return <Avatar src={clerkUser.imageUrl} size="sm" />;
    }
    return (
      <Avatar size="sm" color="blue">
        <IconUser size={16} />
      </Avatar>
    );
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

        <Menu shadow="md" width={200} position="bottom-end">
          <Menu.Target>
            <UnstyledButton>
              <Group gap="xs">
                {getAvatarContent()}
                <Text size="sm" fw={500}>
                  {getDisplayName()}
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
        </Menu>
      </Group>
    </Group>
  );
}
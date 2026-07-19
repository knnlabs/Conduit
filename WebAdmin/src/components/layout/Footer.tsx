'use client';

import { Group, Text, Anchor, Container, Divider } from '@mantine/core';
import { IconBrandGithub, IconLicense, IconBook } from '@tabler/icons-react';

export function Footer() {
  return (
    <footer style={{ marginTop: 'auto' }}>
      <Divider />
      <Container py="md">
        <Group justify="space-between" align="center">
          <Text size="sm" c="dimmed">
            © 2026 Nick Nassiri
          </Text>
          
          <Group gap="md">
            <Anchor
              href="https://github.com/nickna/Conduit"
              target="_blank"
              size="sm"
              c="dimmed"
              style={{ display: 'flex', alignItems: 'center', gap: 4 }}
            >
              <IconBrandGithub size={16} />
              GitHub
            </Anchor>
            
            <Anchor
              href="https://github.com/nickna/Conduit/issues"
              target="_blank"
              size="sm"
              c="dimmed"
              style={{ display: 'flex', alignItems: 'center', gap: 4 }}
            >
              <IconBook size={16} />
              Documentation
            </Anchor>
            
            <Anchor
              href="https://github.com/nickna/Conduit/blob/master/LICENSE"
              size="sm"
              c="dimmed"
              style={{ display: 'flex', alignItems: 'center', gap: 4 }}
            >
              <IconLicense size={16} />
              License
            </Anchor>
          </Group>
        </Group>
      </Container>
    </footer>
  );
}
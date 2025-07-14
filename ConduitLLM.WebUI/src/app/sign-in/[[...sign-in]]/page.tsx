import { SignIn } from '@clerk/nextjs';
import { Center, Container } from '@mantine/core';

export default function SignInPage() {
  return (
    <Container size={420} style={{ minHeight: '100vh', display: 'flex', alignItems: 'center' }}>
      <Center w="100%">
        <SignIn />
      </Center>
    </Container>
  );
}
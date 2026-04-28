import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { ActivityIndicator, View } from 'react-native';
import 'react-native-reanimated';

import { AuthProvider, useAuth } from '@/contexts/authContext';

const queryClient = new QueryClient();

function RootNavigator() {
  const { isLoggedIn } = useAuth();

  // Still hydrating tokens from storage
  if (isLoggedIn === null) {
    return (
      <View style={{ flex: 1, backgroundColor: '#0a0a1e', justifyContent: 'center', alignItems: 'center' }}>
        <ActivityIndicator size="large" color="#39ff14" />
      </View>
    );
  }

  // Not logged in → show login (register is reachable from login)
  if (!isLoggedIn) {
    return (
      <Stack screenOptions={{ headerShown: false }}>
        <Stack.Screen name="login" />
        <Stack.Screen name="register" />
      </Stack>
    );
  }

  // Logged in → show the game
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="(tabs)" />
    </Stack>
  );
}

export default function RootLayout() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <RootNavigator />
        <StatusBar style="light" />
      </AuthProvider>
    </QueryClientProvider>
  );
}

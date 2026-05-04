import { router } from 'expo-router';
import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';

import { useAuth } from '@/contexts/authContext';
import { noAuthApiClient } from '@/helpers/apiClient';
import { authStyles as styles } from '@/styles/auth';

export default function LoginScreen() {
  const { login } = useAuth();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleLogin = async () => {
    if (!username.trim() || !password.trim()) return;
    setError(null);
    setLoading(true);

    try {
      const res = await noAuthApiClient.post('/api/auth/login', {
        username: username.trim(),
        password,
      });

      if (res.status >= 400) {
        setError(res.data?.message ?? 'Login failed');
        return;
      }

      await login(
        { accessToken: res.data.accessToken, refreshToken: res.data.refreshToken },
        {
          playerId: res.data.playerId,
          username: res.data.username,
          level: res.data.level,
          xp: res.data.xp,
        },
      );

      router.replace('/(tabs)');
    } catch {
      setError('Could not reach server');
    } finally {
      setLoading(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <Text style={styles.title}>GEOSLAYER</Text>
      <Text style={styles.subtitle}>SIGN IN TO CONTINUE</Text>

      {error ? <Text style={styles.error}>{error}</Text> : null}

      <TextInput
        style={styles.input}
        placeholder="Username"
        placeholderTextColor="#555"
        autoCapitalize="none"
        autoCorrect={false}
        value={username}
        onChangeText={setUsername}
      />

      <View style={styles.passwordContainer}>
        <TextInput
          style={styles.passwordInput}
          placeholder="Password"
          placeholderTextColor="#555"
          secureTextEntry={!showPassword}
          autoCapitalize="none"
          autoCorrect={false}
          value={password}
          onChangeText={setPassword}
        />
        <TouchableOpacity
          style={styles.eyeButton}
          onPress={() => setShowPassword(!showPassword)}
        >
          <Text style={styles.eyeText}>{showPassword ? '🙈' : '👁️'}</Text>
        </TouchableOpacity>
      </View>

      <TouchableOpacity
        style={[styles.button, loading && styles.buttonDisabled]}
        onPress={handleLogin}
        disabled={loading}
      >
        <Text style={styles.buttonText}>{loading ? 'LOADING...' : 'LOGIN'}</Text>
      </TouchableOpacity>

      <View style={styles.switchRow}>
        <Text style={styles.switchText}>No account?</Text>
        <Text style={styles.switchLink} onPress={() => router.push('/register')}>
          REGISTER
        </Text>
      </View>
    </KeyboardAvoidingView>
  );
}

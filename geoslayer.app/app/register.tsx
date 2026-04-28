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

export default function RegisterScreen() {
  const { login } = useAuth();

  const [username, setUsername] = useState('');
  const [firstName, setFirstName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleRegister = async () => {
    if (!username.trim() || !password.trim() || !firstName.trim() || !email.trim()) return;
    setError(null);
    setLoading(true);

    try {
      // Register
      const regRes = await noAuthApiClient.post('/api/auth/register', {
        username: username.trim(),
        password,
        firstName: firstName.trim(),
        email: email.trim(),
      });

      if (regRes.status >= 400) {
        setError(regRes.data?.message ?? 'Registration failed');
        return;
      }

      // Auto-login after registration
      const loginRes = await noAuthApiClient.post('/api/auth/login', {
        username: username.trim(),
        password,
      });

      if (loginRes.status >= 400) {
        // Registered but login failed – send them to login screen
        router.replace('/login');
        return;
      }

      await login(
        { accessToken: loginRes.data.accessToken, refreshToken: loginRes.data.refreshToken },
        {
          playerId: loginRes.data.playerId,
          username: loginRes.data.username,
          level: loginRes.data.level,
          xp: loginRes.data.xp,
        },
      );
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
      <Text style={styles.subtitle}>CREATE YOUR ACCOUNT</Text>

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

      <TextInput
        style={styles.input}
        placeholder="First Name"
        placeholderTextColor="#555"
        autoCorrect={false}
        value={firstName}
        onChangeText={setFirstName}
      />

      <TextInput
        style={styles.input}
        placeholder="Email"
        placeholderTextColor="#555"
        autoCapitalize="none"
        keyboardType="email-address"
        value={email}
        onChangeText={setEmail}
      />

      <TextInput
        style={styles.input}
        placeholder="Password"
        placeholderTextColor="#555"
        secureTextEntry
        value={password}
        onChangeText={setPassword}
      />

      <TouchableOpacity
        style={[styles.button, loading && styles.buttonDisabled]}
        onPress={handleRegister}
        disabled={loading}
      >
        <Text style={styles.buttonText}>{loading ? 'CREATING...' : 'REGISTER'}</Text>
      </TouchableOpacity>

      <View style={styles.switchRow}>
        <Text style={styles.switchText}>Have an account?</Text>
        <Text style={styles.switchLink} onPress={() => router.push('/login')}>
          LOGIN
        </Text>
      </View>
    </KeyboardAvoidingView>
  );
}

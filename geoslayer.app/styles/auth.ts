import { StyleSheet } from 'react-native';

export const authStyles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0a0a1e',
    justifyContent: 'center',
    paddingHorizontal: 32,
  },
  title: {
    color: '#39ff14',
    fontSize: 36,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    textAlign: 'center',
    marginBottom: 8,
    letterSpacing: 4,
  },
  subtitle: {
    color: '#888',
    fontSize: 12,
    fontFamily: 'monospace',
    textAlign: 'center',
    marginBottom: 40,
  },
  input: {
    backgroundColor: '#1a1a2e',
    borderWidth: 2,
    borderColor: '#39ff14',
    color: '#fff',
    fontFamily: 'monospace',
    fontSize: 14,
    paddingHorizontal: 14,
    paddingVertical: 12,
    marginBottom: 14,
  },
  button: {
    backgroundColor: '#39ff14',
    paddingVertical: 14,
    alignItems: 'center',
    marginTop: 8,
    borderWidth: 2,
    borderColor: '#2ecc40',
  },
  buttonText: {
    color: '#0a0a1e',
    fontSize: 16,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    letterSpacing: 2,
  },
  buttonDisabled: {
    opacity: 0.5,
  },
  switchRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    marginTop: 24,
    gap: 6,
  },
  switchText: {
    color: '#888',
    fontSize: 12,
    fontFamily: 'monospace',
  },
  switchLink: {
    color: '#39ff14',
    fontSize: 12,
    fontFamily: 'monospace',
    fontWeight: 'bold',
  },
  error: {
    color: '#ff2244',
    fontSize: 12,
    fontFamily: 'monospace',
    textAlign: 'center',
    marginBottom: 12,
  },
});

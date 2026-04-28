import Constants from 'expo-constants';

/**
 * Resolves the API base URL.
 * In development the Expo dev-client runs on the local machine,
 * so we point at the .NET backend on localhost.
 * In production builds we hit the real domain.
 */
const DEV_API = 'http://192.168.1.248:5053';
const PROD_API = 'https://gsapi.bregan.me';

export const API_BASE_URL =
  Constants.appOwnership === 'expo' || __DEV__ ? DEV_API : PROD_API;

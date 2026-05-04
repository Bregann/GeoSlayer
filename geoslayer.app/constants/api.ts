/**
 * Resolves the API base URL.
 * For development we point at the hosted API.
 * In production builds we hit the same domain.
 */
const DEV_API = 'https://gsapi.bregan.me';
const PROD_API = 'https://gsapi.bregan.me';

export const API_BASE_URL = __DEV__ ? DEV_API : PROD_API;

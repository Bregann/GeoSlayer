import { keychainHelper } from '@/helpers/keychainHelper';
import AsyncStorage from '@react-native-async-storage/async-storage';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';

/* ------------------------------------------------------------------ */
/*  Types                                                              */
/* ------------------------------------------------------------------ */

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

interface PlayerInfo {
  playerId: number;
  username: string;
  level: number;
  xp: number;
}

interface AuthContextValue {
  /** null while loading from storage, false when logged out */
  isLoggedIn: boolean | null;
  player: PlayerInfo | null;
  /** Set after a successful login/register */
  login: (tokens: AuthTokens, player: PlayerInfo) => Promise<void>;
  logout: () => Promise<void>;
  /** Update player info (e.g. after XP/level change) */
  updatePlayer: (player: PlayerInfo) => Promise<void>;
}

const AuthContext = createContext<AuthContextValue>({
  isLoggedIn: null,
  player: null,
  login: async () => {},
  logout: async () => {},
  updatePlayer: async () => {},
});

/* ------------------------------------------------------------------ */
/*  Storage keys                                                       */
/* ------------------------------------------------------------------ */

const PLAYER_KEY = 'gs_player';

/* ------------------------------------------------------------------ */
/*  Provider                                                           */
/* ------------------------------------------------------------------ */

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isLoggedIn, setIsLoggedIn] = useState<boolean | null>(null);
  const [player, setPlayer] = useState<PlayerInfo | null>(null);

  // Hydrate from storage on mount
  useEffect(() => {
    (async () => {
      try {
        const [isAuth, pl] = await Promise.all([
          keychainHelper.isAuthenticated(),
          AsyncStorage.getItem(PLAYER_KEY),
        ]);
        if (isAuth && pl) {
          setPlayer(JSON.parse(pl));
          setIsLoggedIn(true);
        } else {
          setIsLoggedIn(false);
        }
      } catch {
        setIsLoggedIn(false);
      }
    })();
  }, []);

  const login = useCallback(
    async (newTokens: AuthTokens, newPlayer: PlayerInfo) => {
      await keychainHelper.setAccessToken(newTokens.accessToken);
      await keychainHelper.setRefreshToken(newTokens.refreshToken);
      await AsyncStorage.setItem(PLAYER_KEY, JSON.stringify(newPlayer));
      setPlayer(newPlayer);
      setIsLoggedIn(true);
    },
    [],
  );

  const logout = useCallback(async () => {
    await keychainHelper.deleteTokens();
    await AsyncStorage.removeItem(PLAYER_KEY);
    setPlayer(null);
    setIsLoggedIn(false);
  }, []);

  const updatePlayer = useCallback(async (updated: PlayerInfo) => {
    await AsyncStorage.setItem(PLAYER_KEY, JSON.stringify(updated));
    setPlayer(updated);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      isLoggedIn,
      player,
      login,
      logout,
      updatePlayer,
    }),
    [isLoggedIn, player, login, logout, updatePlayer],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  return useContext(AuthContext);
}

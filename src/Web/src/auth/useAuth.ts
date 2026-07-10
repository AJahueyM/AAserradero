import { InteractionStatus } from '@azure/msal-browser';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { useCallback, useMemo, useState } from 'react';
import { loginRequest } from './msalConfig';

export type LoginErrorKind = 'invalidCredentials' | 'authUnavailable' | 'unknown';
export interface LoginErrorState {
  kind: LoginErrorKind;
  message: string;
}

export function useAuth() {
  const { instance, accounts, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [loginError, setLoginError] = useState<LoginErrorState | null>(() => readRedirectError());
  const activeAccount = useMemo(
    () => instance.getActiveAccount() ?? accounts[0] ?? null,
    [accounts, instance],
  );
  const signIn = useCallback(async () => {
    setLoginError(null);
    try {
      await instance.loginRedirect(loginRequest);
    } catch (error) {
      setLoginError(classifyLoginError(error));
    }
  }, [instance]);
  const signOut = useCallback(async () => {
    await instance.logoutRedirect({ account: activeAccount ?? undefined });
  }, [activeAccount, instance]);
  return {
    activeAccount,
    isAuthenticated,
    isAuthBusy: inProgress !== InteractionStatus.None,
    loginError,
    signIn,
    signOut,
  };
}

function readRedirectError(): LoginErrorState | null {
  const raw = sessionStorage.getItem('auth.redirectError');
  if (!raw) return null;
  sessionStorage.removeItem('auth.redirectError');
  try {
    const parsed = JSON.parse(raw) as { message?: unknown };
    return classifyLoginError(parsed.message ?? raw);
  } catch {
    return classifyLoginError(raw);
  }
}

function classifyLoginError(error: unknown): LoginErrorState {
  const message = error instanceof Error ? error.message : String(error);
  const normalized = message.toLowerCase();
  if (
    normalized.includes('invalid_grant') ||
    normalized.includes('invalid credentials') ||
    normalized.includes('aadb2c90053')
  )
    return { kind: 'invalidCredentials', message };
  if (
    normalized.includes('network') ||
    normalized.includes('server') ||
    normalized.includes('temporarily') ||
    normalized.includes('failed to fetch')
  )
    return { kind: 'authUnavailable', message };
  return { kind: 'unknown', message };
}

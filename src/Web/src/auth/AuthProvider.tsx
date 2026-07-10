import { useMsal } from '@azure/msal-react';
import { useEffect } from 'react';
import { configureHttpClient } from '../api/httpClient';
import { acquireApiAccessToken, loginRequest } from './msalConfig';

interface AuthProviderProps {
  children: React.ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const { instance, accounts } = useMsal();
  useEffect(() => {
    const activeAccount = instance.getActiveAccount() ?? accounts[0];
    if (activeAccount && instance.getActiveAccount()?.homeAccountId !== activeAccount.homeAccountId)
      instance.setActiveAccount(activeAccount);
    configureHttpClient({
      getAccessToken: async () => {
        const account = instance.getActiveAccount() ?? accounts[0];
        if (!account) {
          await instance.loginRedirect(loginRequest);
          throw new Error('No active account is available.');
        }
        return acquireApiAccessToken(instance, account);
      },
      onUnauthorized: async () => {
        await instance.loginRedirect(loginRequest);
      },
    });
  }, [accounts, instance]);
  return children;
}

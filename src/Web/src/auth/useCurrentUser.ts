import { useIsAuthenticated } from '@azure/msal-react';
import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/httpClient';
import { normalizeCapabilities, type Capability } from './capabilities';

export interface CurrentUser {
  id: string;
  displayName: string;
  capabilities: Capability[];
}
interface CurrentUserResponse {
  id: string;
  displayName: string;
  capabilities: string[];
}

export function useCurrentUser() {
  const isAuthenticated = useIsAuthenticated();
  return useQuery({
    queryKey: ['current-user'],
    queryFn: async (): Promise<CurrentUser> => {
      const response = await apiGet<CurrentUserResponse>('/api/me');
      return { ...response, capabilities: normalizeCapabilities(response.capabilities) };
    },
    enabled: isAuthenticated,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}
